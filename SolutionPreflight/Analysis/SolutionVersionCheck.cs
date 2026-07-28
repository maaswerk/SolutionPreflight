using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk.Query;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// Two version-related import failures that are trivial to predict but easy to trip over:
    ///
    /// - Dataverse rejects a solution/patch whose version isn't strictly higher than what's already
    ///   installed in the target (errors 80048539/8004F042, "Patch version X is invalid for parent
    ///   version Y" / "already exists. Updating patch is not supported").
    /// - A previous failed upgrade can leave a "&lt;solution&gt;_Upgrade" holding solution behind in the
    ///   target, which blocks any further upgrade/patch attempt until it's resolved.
    /// </summary>
    public class SolutionVersionCheck : IPreflightCheck
    {
        public string Name => "Solution Version";

        public string Category => "SolutionVersion";

        public IEnumerable<PreflightFinding> Run(PreflightContext context)
        {
            var findings = new List<PreflightFinding>();

            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("version")
            };
            query.Criteria.AddCondition("uniquename", ConditionOperator.Equal, context.SourceSolution.UniqueName);
            var targetMatches = context.TargetService.RetrieveMultiple(query);

            if (targetMatches.Entities.Count > 0)
            {
                var targetVersionRaw = targetMatches.Entities[0].GetAttributeValue<string>("version");
                if (!string.IsNullOrEmpty(targetVersionRaw) && !string.IsNullOrEmpty(context.SourceSolution.Version) &&
                    Version.TryParse(context.SourceSolution.Version, out var sourceVersion) &&
                    Version.TryParse(targetVersionRaw, out var targetVersion) &&
                    sourceVersion <= targetVersion)
                {
                    findings.Add(new PreflightFinding
                    {
                        Severity = Severity.Blocker,
                        Category = Category,
                        ComponentName = context.SourceSolution.UniqueName,
                        ComponentType = "Solution",
                        Message = $"The source solution's version ({sourceVersion}) is not higher than the version already " +
                                  $"installed in the target ({targetVersion}). Dataverse rejects an update whose version isn't " +
                                  "strictly greater than what's currently installed.",
                        SuggestedFix = "Bump the solution's version in the source above the target's installed version before exporting/importing.",
                        CheckName = Name
                    });
                }
            }

            var holdingQuery = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("uniquename")
            };
            holdingQuery.Criteria.AddCondition("uniquename", ConditionOperator.Equal, $"{context.SourceSolution.UniqueName}_Upgrade");
            var holdingMatches = context.TargetService.RetrieveMultiple(holdingQuery);

            if (holdingMatches.Entities.Count > 0)
            {
                findings.Add(new PreflightFinding
                {
                    Severity = Severity.Blocker,
                    Category = Category,
                    ComponentName = context.SourceSolution.UniqueName,
                    ComponentType = "Solution",
                    Message = $"A holding solution '{context.SourceSolution.UniqueName}_Upgrade' already exists in the target, " +
                              "left over from a previously failed upgrade attempt.",
                    SuggestedFix = "Complete or delete the pending holding solution in the target (Settings > Solutions) before retrying this import.",
                    CheckName = Name
                });
            }

            return findings;
        }
    }
}
