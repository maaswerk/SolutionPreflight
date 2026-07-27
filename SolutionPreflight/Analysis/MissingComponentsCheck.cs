using System.Collections.Generic;
using Microsoft.Crm.Sdk.Messages;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// Core check: runs <see cref="RetrieveMissingComponentsRequest"/> against the target with the
    /// source solution's exported customization file. Catches the majority of "import will fail"
    /// dependency problems (missing entities, attributes, option sets, referenced components, ...).
    /// </summary>
    public class MissingComponentsCheck : IPreflightCheck
    {
        public string Name => "Missing Components";

        public string Category => "MissingComponents";

        public IEnumerable<PreflightFinding> Run(PreflightContext context)
        {
            var findings = new List<PreflightFinding>();

            if (context.CustomizationFile == null || context.CustomizationFile.Length == 0)
            {
                findings.Add(new PreflightFinding(
                    Severity.Info,
                    Category,
                    context.SourceSolution.UniqueName,
                    "No customization file was exported for this solution, missing-components check was skipped.",
                    "Re-run the analysis; if the export keeps failing check the source connection and solution size."));
                return findings;
            }

            var request = new RetrieveMissingComponentsRequest
            {
                CustomizationFile = context.CustomizationFile
            };

            var response = (RetrieveMissingComponentsResponse)context.TargetService.Execute(request);

            var missingComponents = response.MissingComponents;
            if (missingComponents == null || missingComponents.Length == 0)
            {
                findings.Add(new PreflightFinding(
                    Severity.Info,
                    Category,
                    context.SourceSolution.UniqueName,
                    "RetrieveMissingComponentsRequest reported no missing dependencies in the target.",
                    null)
                { CheckName = Name });
                return findings;
            }

            foreach (var mc in missingComponents)
            {
                var required = mc.RequiredComponent;
                var displayName = required?.DisplayName ?? required?.SchemaName ?? "(unknown)";
                var parentDisplayName = mc.DependentComponent?.DisplayName ?? mc.DependentComponent?.SchemaName;
                var componentType = DescribeType(required);

                var message = string.IsNullOrEmpty(parentDisplayName)
                    ? $"Component '{displayName}' ({componentType}) required by the solution is missing in the target."
                    : $"Component '{displayName}' ({componentType}), required by '{parentDisplayName}', is missing in the target.";

                findings.Add(new PreflightFinding
                {
                    Severity = Severity.Blocker,
                    Category = Category,
                    ComponentName = displayName,
                    ComponentType = componentType,
                    Message = message,
                    SuggestedFix = "Add the missing component (or its parent solution) to the target before importing, " +
                                   "or add it as an additional solution component in the source solution.",
                    CheckName = Name
                });
            }

            return findings;
        }

        private static string DescribeType(ComponentDetail component)
        {
            return component == null ? "unknown type" : $"type {component.Type}";
        }
    }
}
