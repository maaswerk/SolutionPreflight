using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json.Linq;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// Cloud Flows (`workflow` records with category = Modern Flow) are always imported in a
    /// switched-off state, regardless of their state in the source - this is standard Dataverse
    /// import behavior, not a bug. This check surfaces every active flow in the solution so it
    /// doesn't get forgotten after import, together with the owner-assignment step the import
    /// dialog will require.
    ///
    /// It also closes a gap <see cref="ConnectionReferenceCheck"/> can't see on its own: that check
    /// only looks at connection references that are *solution components*. A flow can keep depending
    /// on a connection reference internally (it's baked into the flow's `clientdata` JSON) even after
    /// that connection reference has been removed from the solution's component list - which then
    /// silently fails to activate in a target that doesn't already have it, with no warning from a
    /// component-list-only check. So this check parses each flow's `clientdata` for the connection
    /// references it actually uses and validates those directly against the target, specifically for
    /// the ones NOT tracked by the solution (the ones that are tracked are already covered by
    /// <see cref="ConnectionReferenceCheck"/>).
    /// </summary>
    public class CloudFlowCheck : IPreflightCheck
    {
        private const int ModernFlowCategory = 5;
        private const int StateActivated = 1;

        public string Name => "Cloud Flow Activation";

        public string Category => "CloudFlow";

        public IEnumerable<PreflightFinding> Run(PreflightContext context)
        {
            var findings = new List<PreflightFinding>();

            var flows = SolutionComponentHelper.GetSolutionLinkedRecords(
                context.SourceService,
                context.SourceSolution.SolutionId,
                "workflow",
                "workflowid",
                "workflowid", "name", "category", "statecode", "clientdata");

            var solutionTrackedConnectionRefs = GetSolutionConnectionReferenceLogicalNames(context);

            foreach (var flow in flows.Entities)
            {
                var category = flow.GetAttributeValue<OptionSetValue>("category")?.Value;
                if (category != ModernFlowCategory)
                {
                    continue;
                }

                var name = flow.GetAttributeValue<string>("name") ?? "(unnamed flow)";
                var state = flow.GetAttributeValue<OptionSetValue>("statecode")?.Value;

                if (state == StateActivated)
                {
                    findings.Add(new PreflightFinding
                    {
                        Severity = Severity.Warning,
                        Category = Category,
                        ComponentName = name,
                        ComponentType = "Cloud Flow",
                        Message = $"Cloud Flow '{name}' is turned on in the source. Solution import always brings flows in " +
                                  "turned off in the target, and the import dialog will ask for a new owner.",
                        SuggestedFix = "After import: assign an owner to the flow, verify its connection references (see the " +
                                       "Connection Reference findings), then turn it back on.",
                        CheckName = Name
                    });
                }
                else
                {
                    findings.Add(new PreflightFinding
                    {
                        Severity = Severity.Info,
                        Category = Category,
                        ComponentName = name,
                        ComponentType = "Cloud Flow",
                        Message = $"Cloud Flow '{name}' is already turned off in the source.",
                        SuggestedFix = "No action required unless this flow is expected to run in the target.",
                        CheckName = Name
                    });
                }

                var clientData = flow.GetAttributeValue<string>("clientdata");
                var usedConnectionRefs = ExtractConnectionReferenceLogicalNames(clientData);

                foreach (var logicalName in usedConnectionRefs)
                {
                    if (solutionTrackedConnectionRefs.Contains(logicalName))
                    {
                        // Already covered by ConnectionReferenceCheck - avoid duplicate findings.
                        continue;
                    }

                    var status = GetTargetConnectionReferenceStatus(context, logicalName);

                    switch (status)
                    {
                        case ConnectionReferenceStatus.Missing:
                            findings.Add(new PreflightFinding
                            {
                                Severity = Severity.Blocker,
                                Category = Category,
                                ComponentName = name,
                                ComponentType = "Cloud Flow",
                                Message = $"Cloud Flow '{name}' internally depends on connection reference '{logicalName}', which is " +
                                          "not included in this solution and does not exist in the target - it will not be created by " +
                                          "this import, so the flow cannot be activated.",
                                SuggestedFix = $"Add '{logicalName}' as a component of this solution, or make sure it already exists " +
                                               "(with a connection set) in the target before importing.",
                                CheckName = Name
                            });
                            break;
                        case ConnectionReferenceStatus.ExistsUnmapped:
                            findings.Add(new PreflightFinding
                            {
                                Severity = Severity.Warning,
                                Category = Category,
                                ComponentName = name,
                                ComponentType = "Cloud Flow",
                                Message = $"Cloud Flow '{name}' internally depends on connection reference '{logicalName}' (not tracked " +
                                          "by this solution), which exists in the target but has no connection configured.",
                                SuggestedFix = "Set a connection on it in the target before turning this flow on.",
                                CheckName = Name
                            });
                            break;
                        case ConnectionReferenceStatus.ExistsMapped:
                            findings.Add(new PreflightFinding
                            {
                                Severity = Severity.Info,
                                Category = Category,
                                ComponentName = name,
                                ComponentType = "Cloud Flow",
                                Message = $"Cloud Flow '{name}' internally depends on connection reference '{logicalName}', which isn't " +
                                          "a component of this solution but is already present and configured in the target.",
                                SuggestedFix = "Fine for now - if this solution is ever imported into a different environment, add " +
                                               $"'{logicalName}' as a component too, or pre-create it there first.",
                                CheckName = Name
                            });
                            break;
                    }
                }
            }

            return findings;
        }

        private enum ConnectionReferenceStatus
        {
            Missing,
            ExistsUnmapped,
            ExistsMapped
        }

        private static ConnectionReferenceStatus GetTargetConnectionReferenceStatus(PreflightContext context, string logicalName)
        {
            var query = new QueryExpression("connectionreference")
            {
                ColumnSet = new ColumnSet("connectionid")
            };
            query.Criteria.AddCondition("connectionreferencelogicalname", ConditionOperator.Equal, logicalName);
            var matches = context.TargetService.RetrieveMultiple(query);

            if (matches.Entities.Count == 0)
            {
                return ConnectionReferenceStatus.Missing;
            }

            var connectionId = matches.Entities[0].GetAttributeValue<string>("connectionid");
            return string.IsNullOrEmpty(connectionId) ? ConnectionReferenceStatus.ExistsUnmapped : ConnectionReferenceStatus.ExistsMapped;
        }

        private static HashSet<string> GetSolutionConnectionReferenceLogicalNames(PreflightContext context)
        {
            var refs = SolutionComponentHelper.GetSolutionLinkedRecords(
                context.SourceService,
                context.SourceSolution.SolutionId,
                "connectionreference",
                "connectionreferenceid",
                "connectionreferencelogicalname");

            return new HashSet<string>(
                refs.Entities
                    .Select(e => e.GetAttributeValue<string>("connectionreferencelogicalname"))
                    .Where(n => !string.IsNullOrEmpty(n)),
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Pulls every `connectionReferenceLogicalName` out of a Cloud Flow's `clientdata` JSON
        /// (shape: <c>properties.connectionReferences.&lt;key&gt;.connection.connectionReferenceLogicalName</c>).
        /// Defensive by design: an unexpected/malformed shape degrades to "no dependencies found"
        /// instead of failing the whole analysis run.
        /// </summary>
        private static List<string> ExtractConnectionReferenceLogicalNames(string clientData)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(clientData))
            {
                return result;
            }

            try
            {
                var root = JObject.Parse(clientData);
                if (!(root["properties"]?["connectionReferences"] is JObject connectionReferences))
                {
                    return result;
                }

                foreach (var property in connectionReferences.Properties())
                {
                    var logicalName = property.Value["connection"]?["connectionReferenceLogicalName"]?.ToString();
                    if (!string.IsNullOrEmpty(logicalName))
                    {
                        result.Add(logicalName);
                    }
                }
            }
            catch
            {
                // Malformed/unexpected clientdata shape - degrade gracefully.
            }

            return result;
        }
    }
}
