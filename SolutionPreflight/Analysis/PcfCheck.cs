using System.Collections.Generic;
using Microsoft.Xrm.Sdk.Query;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// PCF (custom control) components already surface as missing via <see cref="MissingComponentsCheck"/>
    /// when they don't exist at all in the target. What that message does not tell you is whether an
    /// *older* version of the same control is already registered from another solution, which can lead
    /// to a manifest mismatch after import. A full manifest/version diff needs the control bundle
    /// itself and is out of scope here (see CLAUDE.md, open points) - this check only flags the
    /// situation so it can be verified manually.
    /// </summary>
    public class PcfCheck : IPreflightCheck
    {
        public string Name => "PCF Controls";

        public string Category => "Pcf";

        public IEnumerable<PreflightFinding> Run(PreflightContext context)
        {
            var findings = new List<PreflightFinding>();

            var controls = SolutionComponentHelper.GetSolutionLinkedRecords(
                context.SourceService,
                context.SourceSolution.SolutionId,
                "customcontrol",
                "customcontrolid",
                "customcontrolid", "name");

            foreach (var control in controls.Entities)
            {
                var name = control.GetAttributeValue<string>("name");
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var query = new QueryExpression("customcontrol")
                {
                    ColumnSet = new ColumnSet("customcontrolid")
                };
                query.Criteria.AddCondition("name", ConditionOperator.Equal, name);
                var existing = context.TargetService.RetrieveMultiple(query);

                if (existing.Entities.Count > 0)
                {
                    findings.Add(new PreflightFinding
                    {
                        Severity = Severity.Info,
                        Category = Category,
                        ComponentName = name,
                        ComponentType = "PCF Control",
                        Message = $"PCF control '{name}' is already registered in the target (possibly by another solution).",
                        SuggestedFix = "Confirm the version/manifest being imported is compatible with what's already deployed; " +
                                       "this tool cannot diff PCF bundle versions automatically.",
                        CheckName = Name
                    });
                }
            }

            return findings;
        }
    }
}
