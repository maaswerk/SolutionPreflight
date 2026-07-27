using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// Cloud Flows (`workflow` records with category = Modern Flow) are always imported in a
    /// switched-off state, regardless of their state in the source - this is standard Dataverse
    /// import behavior, not a bug. This check surfaces every active flow in the solution so it
    /// doesn't get forgotten after import, together with the owner-assignment step the import
    /// dialog will require.
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
                "workflowid", "name", "category", "statecode");

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
            }

            return findings;
        }
    }
}
