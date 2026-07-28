using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// For every component this solution touches, asks the target directly (via
    /// <c>RetrieveDependentComponentsRequest</c>) whether some *other* solution's component currently
    /// depends on it there. That is precisely the situation behind Dataverse error 8004F020
    /// ("the uninstall operation will delete the base layer for component '...' ... blocked by other
    /// managed layers") and the various "solution dependencies exist, cannot uninstall/delete" errors:
    /// a later update/removal of this component can be blocked by, or break, a completely unrelated
    /// solution nobody remembers still relies on it.
    ///
    /// The response's `dependency` records carry <c>dependentcomponentbasesolutionid</c> - the solution
    /// that owns the *dependent* (the thing relying on our component) - which is exactly what lets this
    /// check tell "a component from this same solution" apart from "a component from some other,
    /// unrelated solution" without needing to interpret the numeric component-type codes at all.
    /// </summary>
    public class DependentComponentsCheck : IPreflightCheck
    {
        private const int BatchSize = 50;

        public string Name => "Dependent Components";

        public string Category => "Dependents";

        public IEnumerable<PreflightFinding> Run(PreflightContext context)
        {
            var findings = new List<PreflightFinding>();

            var components = GetSourceComponents(context);
            if (components.Count == 0)
            {
                return findings;
            }

            var ownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(context.SourceSolution.UniqueName))
            {
                ownNames.Add(context.SourceSolution.UniqueName);
            }

            if (!string.IsNullOrEmpty(context.SourceSolution.FriendlyName))
            {
                ownNames.Add(context.SourceSolution.FriendlyName);
            }

            var displayInfo = GetComponentDisplayInfo(context.TargetService, components.Select(c => c.ObjectId).ToList());
            var batches = Batch(components, BatchSize).ToList();

            for (var b = 0; b < batches.Count; b++)
            {
                var batch = batches[b];
                context.ReportProgress($"Dependent Components: checking {batch.Count} component(s) in target (batch {b + 1}/{batches.Count})...");

                var request = new ExecuteMultipleRequest
                {
                    Settings = new ExecuteMultipleSettings { ContinueOnError = true, ReturnResponses = true },
                    Requests = new OrganizationRequestCollection()
                };

                foreach (var component in batch)
                {
                    request.Requests.Add(new RetrieveDependentComponentsRequest
                    {
                        ComponentType = component.ComponentType,
                        ObjectId = component.ObjectId
                    });
                }

                ExecuteMultipleResponse response;
                try
                {
                    response = (ExecuteMultipleResponse)context.TargetService.Execute(request);
                }
                catch (Exception)
                {
                    continue; // don't let one bad batch take down the whole check
                }

                for (var i = 0; i < batch.Count; i++)
                {
                    var itemResponse = response.Responses.FirstOrDefault(r => r.RequestIndex == i);
                    if (itemResponse?.Fault != null || !(itemResponse?.Response is RetrieveDependentComponentsResponse dependentResponse))
                    {
                        continue;
                    }

                    var dependents = dependentResponse.EntityCollection?.Entities;
                    if (dependents == null || dependents.Count == 0)
                    {
                        continue;
                    }

                    var foreignSolutionIds = dependents
                        .Select(e => e.GetAttributeValue<Guid>("dependentcomponentbasesolutionid"))
                        .Where(id => id != Guid.Empty)
                        .Distinct()
                        .ToList();

                    if (foreignSolutionIds.Count == 0)
                    {
                        continue;
                    }

                    var solutionNames = ResolveSolutionNames(context.TargetService, foreignSolutionIds)
                        .Values
                        .Where(n => !string.IsNullOrEmpty(n) && !ownNames.Contains(n))
                        .Distinct()
                        .ToList();

                    if (solutionNames.Count == 0)
                    {
                        continue;
                    }

                    var component = batch[i];
                    displayInfo.TryGetValue(component.ObjectId, out var info);
                    var hasFriendlyName = !string.IsNullOrEmpty(info.Name);
                    var componentLabel = hasFriendlyName ? $"{info.Name} ({info.TypeName})" : $"component {component.ObjectId}";
                    var solutionList = string.Join(", ", solutionNames);

                    findings.Add(new PreflightFinding
                    {
                        Severity = Severity.Warning,
                        Category = Category,
                        ComponentName = componentLabel,
                        ComponentType = hasFriendlyName ? info.TypeName : "Solution Component",
                        Message = $"In the target, '{componentLabel}' is currently depended on by component(s) from other, " +
                                  $"unrelated solution(s): {solutionList}. If this import changes, replaces, or removes this " +
                                  "component, those solutions can block the operation later or break outright (Dataverse error " +
                                  "8004F020 and similar \"solution dependencies exist\" errors).",
                        SuggestedFix = $"Review {solutionList} before importing, especially if this component's behavior is " +
                                       "changing significantly - coordinate with whoever owns those solutions first.",
                        CheckName = Name
                    });
                }
            }

            return findings;
        }

        private static List<(Guid ObjectId, int ComponentType)> GetSourceComponents(PreflightContext context)
        {
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid", "componenttype")
            };
            query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, context.SourceSolution.SolutionId);

            var components = context.SourceService.RetrieveMultiple(query);
            return components.Entities
                .Select(e => (
                    ObjectId: e.GetAttributeValue<Guid>("objectid"),
                    ComponentType: e.GetAttributeValue<OptionSetValue>("componenttype")?.Value ?? -1))
                .Where(c => c.ObjectId != Guid.Empty && c.ComponentType != -1)
                .Distinct()
                .ToList();
        }

        private static Dictionary<Guid, (string Name, string TypeName)> GetComponentDisplayInfo(IOrganizationService targetService, List<Guid> objectIds)
        {
            var result = new Dictionary<Guid, (string, string)>();

            foreach (var batch in Batch(objectIds, 500))
            {
                var query = new QueryExpression("msdyn_componentlayer")
                {
                    ColumnSet = new ColumnSet("msdyn_componentid", "msdyn_name", "msdyn_solutioncomponentname")
                };
                query.Criteria.AddCondition("msdyn_componentid", ConditionOperator.In, batch.Cast<object>().ToArray());

                EntityCollection layers;
                try
                {
                    layers = targetService.RetrieveMultiple(query);
                }
                catch (Exception)
                {
                    continue; // display names are a nice-to-have, not load-bearing
                }

                foreach (var layer in layers.Entities)
                {
                    var id = layer.GetAttributeValue<Guid>("msdyn_componentid");
                    if (!result.ContainsKey(id))
                    {
                        result[id] = (layer.GetAttributeValue<string>("msdyn_name"), layer.GetAttributeValue<string>("msdyn_solutioncomponentname"));
                    }
                }
            }

            return result;
        }

        private static Dictionary<Guid, string> ResolveSolutionNames(IOrganizationService service, List<Guid> solutionIds)
        {
            var result = new Dictionary<Guid, string>();
            if (solutionIds.Count == 0)
            {
                return result;
            }

            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("solutionid", "friendlyname", "uniquename")
            };
            query.Criteria.AddCondition("solutionid", ConditionOperator.In, solutionIds.Cast<object>().ToArray());

            EntityCollection matches;
            try
            {
                matches = service.RetrieveMultiple(query);
            }
            catch (Exception)
            {
                return result;
            }

            foreach (var solution in matches.Entities)
            {
                var name = solution.GetAttributeValue<string>("friendlyname") ?? solution.GetAttributeValue<string>("uniquename");
                result[solution.Id] = name;
            }

            return result;
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
