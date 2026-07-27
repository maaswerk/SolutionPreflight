using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SolutionPreflight.Models;

namespace SolutionPreflight.Layers
{
    public class LayerRemovalResult
    {
        public ComponentLayerInfo Layer { get; set; }

        public bool Succeeded { get; set; }

        public string Error { get; set; }
    }

    /// <summary>
    /// Reads active solution component layers and removes them in bulk via the `RemoveActiveCustomization`
    /// Web API action (parameters `LogicalName` + `Id`) - the same operation the maker portal's
    /// "Remove Active Customization" command uses.
    ///
    /// Only component types in <see cref="SupportedLogicalNames"/> are considered removable: those are
    /// the types where the layer's `msdyn_componentid` is directly a normal table row id, so the mapping
    /// to a Web API `LogicalName` is unambiguous. Metadata-backed types (Entity, Attribute, Relationship,
    /// OptionSet) and anything not explicitly whitelisted are intentionally left out - removing those
    /// needs a different, metadata-id-based call this tool does not attempt to guess (see CLAUDE.md,
    /// open points). Those layers still show up in the grid but are flagged as not removable here.
    /// </summary>
    public class SolutionLayerService
    {
        private const int BatchSize = 100;
        private const int MaxRetriesPerBatch = 4;

        /// <summary>
        /// Normalized (lowercase, spaces stripped) component-type label -> Web API entity logical name,
        /// for the component types this tool will bulk-remove active layers for.
        /// </summary>
        private static readonly Dictionary<string, string> SupportedLogicalNames = new Dictionary<string, string>
        {
            ["form"] = "systemform",
            ["systemform"] = "systemform",
            ["savedquery"] = "savedquery",
            ["view"] = "savedquery",
            ["workflow"] = "workflow",
            ["process"] = "workflow",
            ["webresource"] = "webresource",
            ["role"] = "role",
            ["securityrole"] = "role",
            ["sitemap"] = "sitemap",
            ["connectionreference"] = "connectionreference",
            ["environmentvariabledefinition"] = "environmentvariabledefinition",
            ["report"] = "report",
            ["fieldsecurityprofile"] = "fieldsecurityprofile",
            ["customcontrol"] = "customcontrol",
            ["pcfcontrol"] = "customcontrol",
        };

        public List<ComponentLayerInfo> GetLayers(IOrganizationService sourceService, IOrganizationService targetService, Guid sourceSolutionId)
        {
            var componentIds = GetSourceComponentIds(sourceService, sourceSolutionId);
            var result = new List<ComponentLayerInfo>();
            if (componentIds.Count == 0)
            {
                return result;
            }

            var unmanagedSolutionNames = GetUnmanagedSolutionNames(targetService);

            foreach (var batch in Batch(componentIds, 500))
            {
                var query = new QueryExpression("msdyn_componentlayer")
                {
                    ColumnSet = new ColumnSet(
                        "msdyn_componentlayerid", "msdyn_componentid", "msdyn_name", "msdyn_solutioncomponentname",
                        "msdyn_solutionname", "msdyn_publishername", "msdyn_endtime")
                };
                query.Criteria.AddCondition("msdyn_componentid", ConditionOperator.In, batch.Cast<object>().ToArray());
                query.Criteria.AddCondition("msdyn_endtime", ConditionOperator.Null);

                var layers = targetService.RetrieveMultiple(query);
                foreach (var layer in layers.Entities)
                {
                    var typeName = layer.GetAttributeValue<string>("msdyn_solutioncomponentname") ?? "Unknown";
                    var solutionName = layer.GetAttributeValue<string>("msdyn_solutionname") ?? string.Empty;

                    result.Add(new ComponentLayerInfo
                    {
                        ComponentLayerId = layer.Id,
                        ComponentId = layer.GetAttributeValue<Guid>("msdyn_componentid"),
                        ComponentTypeName = typeName,
                        ComponentName = layer.GetAttributeValue<string>("msdyn_name") ?? layer.GetAttributeValue<Guid>("msdyn_componentid").ToString(),
                        SolutionName = solutionName,
                        PublisherName = layer.GetAttributeValue<string>("msdyn_publishername"),
                        IsUnmanaged = unmanagedSolutionNames.Contains(solutionName),
                        CanRemove = ResolveLogicalName(typeName) != null
                    });
                }
            }

            return result;
        }

        public List<LayerRemovalResult> RemoveActiveLayers(IOrganizationService targetService, IEnumerable<ComponentLayerInfo> layers, Action<string> reportProgress = null)
        {
            reportProgress = reportProgress ?? (_ => { });
            var removable = layers.Where(l => l.CanRemove).ToList();
            var results = new List<LayerRemovalResult>();

            var batches = Batch(removable, BatchSize).ToList();
            for (var b = 0; b < batches.Count; b++)
            {
                var batch = batches[b];
                reportProgress($"Removing active layers, batch {b + 1}/{batches.Count} ({batch.Count} components)...");

                var request = new ExecuteMultipleRequest
                {
                    Settings = new ExecuteMultipleSettings { ContinueOnError = true, ReturnResponses = true },
                    Requests = new OrganizationRequestCollection()
                };

                foreach (var layer in batch)
                {
                    var logicalName = ResolveLogicalName(layer.ComponentTypeName);
                    var req = new OrganizationRequest("RemoveActiveCustomization")
                    {
                        Parameters = new ParameterCollection
                        {
                            ["LogicalName"] = logicalName,
                            ["Id"] = layer.ComponentId
                        }
                    };
                    request.Requests.Add(req);
                }

                var response = ExecuteWithThrottleRetry(targetService, request);

                for (var i = 0; i < batch.Count; i++)
                {
                    var itemResponse = response.Responses.FirstOrDefault(r => r.RequestIndex == i);
                    results.Add(new LayerRemovalResult
                    {
                        Layer = batch[i],
                        Succeeded = itemResponse?.Fault == null,
                        Error = itemResponse?.Fault?.Message
                    });
                }
            }

            return results;
        }

        private static ExecuteMultipleResponse ExecuteWithThrottleRetry(IOrganizationService service, ExecuteMultipleRequest request)
        {
            var attempt = 0;
            while (true)
            {
                try
                {
                    return (ExecuteMultipleResponse)service.Execute(request);
                }
                catch (Exception ex) when (attempt < MaxRetriesPerBatch && IsThrottling(ex))
                {
                    attempt++;
                    var delaySeconds = (int)Math.Pow(2, attempt) * 5;
                    Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
                }
            }
        }

        private static bool IsThrottling(Exception ex)
        {
            var message = ex.Message ?? string.Empty;
            return message.IndexOf("429", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("Number of requests", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("throttl", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ResolveLogicalName(string componentTypeName)
        {
            if (string.IsNullOrEmpty(componentTypeName))
            {
                return null;
            }

            var normalized = new string(componentTypeName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            return SupportedLogicalNames.TryGetValue(normalized, out var logicalName) ? logicalName : null;
        }

        private static List<Guid> GetSourceComponentIds(IOrganizationService sourceService, Guid sourceSolutionId)
        {
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid")
            };
            query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, sourceSolutionId);

            var components = sourceService.RetrieveMultiple(query);
            return components.Entities
                .Select(e => e.GetAttributeValue<Guid>("objectid"))
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();
        }

        private static HashSet<string> GetUnmanagedSolutionNames(IOrganizationService targetService)
        {
            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("uniquename", "friendlyname")
            };
            query.Criteria.AddCondition("ismanaged", ConditionOperator.Equal, false);

            var solutions = targetService.RetrieveMultiple(query);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var solution in solutions.Entities)
            {
                var uniqueName = solution.GetAttributeValue<string>("uniquename");
                var friendlyName = solution.GetAttributeValue<string>("friendlyname");
                if (!string.IsNullOrEmpty(uniqueName)) names.Add(uniqueName);
                if (!string.IsNullOrEmpty(friendlyName)) names.Add(friendlyName);
            }

            return names;
        }

        private static IEnumerable<List<T>> Batch<T>(List<T> source, int size)
        {
            for (var i = 0; i < source.Count; i += size)
            {
                yield return source.GetRange(i, Math.Min(size, source.Count - i));
            }
        }
    }
}
