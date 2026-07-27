using System.Collections.Generic;
using Microsoft.Xrm.Sdk.Query;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// If a solution with the same unique name already exists in the target under a *different*
    /// publisher, Dataverse rejects the import outright. This is easy to miss when the source
    /// solution was re-created with a new publisher at some point.
    /// </summary>
    public class PublisherMismatchCheck : IPreflightCheck
    {
        public string Name => "Publisher Mismatch";

        public string Category => "Publisher";

        public IEnumerable<PreflightFinding> Run(PreflightContext context)
        {
            var findings = new List<PreflightFinding>();

            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("solutionid", "publisherid")
            };
            query.Criteria.AddCondition("uniquename", ConditionOperator.Equal, context.SourceSolution.UniqueName);
            var link = query.AddLink("publisher", "publisherid", "publisherid", JoinOperator.Inner);
            link.EntityAlias = "pub";
            link.Columns = new ColumnSet("uniquename", "customizationprefix");

            var matches = context.TargetService.RetrieveMultiple(query);
            if (matches.Entities.Count == 0)
            {
                findings.Add(new PreflightFinding
                {
                    Severity = Severity.Info,
                    Category = Category,
                    ComponentName = context.SourceSolution.UniqueName,
                    Message = $"Solution '{context.SourceSolution.UniqueName}' does not exist in the target yet - this will be a first-time import.",
                    SuggestedFix = null,
                    CheckName = Name
                });
                return findings;
            }

            var targetSolution = matches.Entities[0];
            var targetPrefix = targetSolution.GetAttributeValue<Microsoft.Xrm.Sdk.AliasedValue>("pub.customizationprefix")?.Value as string;
            var targetPublisherUniqueName = targetSolution.GetAttributeValue<Microsoft.Xrm.Sdk.AliasedValue>("pub.uniquename")?.Value as string;

            if (!string.IsNullOrEmpty(targetPrefix) &&
                !string.IsNullOrEmpty(context.SourceSolution.PublisherPrefix) &&
                !string.Equals(targetPrefix, context.SourceSolution.PublisherPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new PreflightFinding
                {
                    Severity = Severity.Blocker,
                    Category = Category,
                    ComponentName = context.SourceSolution.UniqueName,
                    Message = $"Solution '{context.SourceSolution.UniqueName}' already exists in the target under publisher " +
                              $"'{targetPublisherUniqueName}' (prefix '{targetPrefix}'), but the source solution's publisher prefix is " +
                              $"'{context.SourceSolution.PublisherPrefix}'. Dataverse will reject this import.",
                    SuggestedFix = "Align the publisher (and prefix) between source and target, or rename/recreate the solution " +
                                   "under the publisher that is already installed in the target.",
                    CheckName = Name
                });
            }

            return findings;
        }
    }
}
