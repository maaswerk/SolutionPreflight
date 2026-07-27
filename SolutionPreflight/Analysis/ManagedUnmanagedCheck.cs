using System.Collections.Generic;
using Microsoft.Xrm.Sdk.Query;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// A solution's managed/unmanaged type cannot be changed by re-importing over an existing
    /// installation of the other type - Dataverse rejects it. Catching this before export/import
    /// saves a failed import attempt.
    /// </summary>
    public class ManagedUnmanagedCheck : IPreflightCheck
    {
        public string Name => "Managed / Unmanaged";

        public string Category => "SolutionType";

        public IEnumerable<PreflightFinding> Run(PreflightContext context)
        {
            var findings = new List<PreflightFinding>();

            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("solutionid", "ismanaged")
            };
            query.Criteria.AddCondition("uniquename", ConditionOperator.Equal, context.SourceSolution.UniqueName);
            var matches = context.TargetService.RetrieveMultiple(query);

            if (matches.Entities.Count == 0)
            {
                return findings;
            }

            var targetIsManaged = matches.Entities[0].GetAttributeValue<bool>("ismanaged");
            if (targetIsManaged != context.ImportAsManaged)
            {
                findings.Add(new PreflightFinding
                {
                    Severity = Severity.Blocker,
                    Category = Category,
                    ComponentName = context.SourceSolution.UniqueName,
                    Message = $"Solution '{context.SourceSolution.UniqueName}' is already installed in the target as " +
                              $"{(targetIsManaged ? "managed" : "unmanaged")}, but you intend to import it as " +
                              $"{(context.ImportAsManaged ? "managed" : "unmanaged")}. Dataverse does not allow changing the " +
                              "solution type by re-importing.",
                    SuggestedFix = targetIsManaged
                        ? "Import as managed, or uninstall the managed solution in the target first if unmanaged is truly required."
                        : "Import as unmanaged, or delete the unmanaged solution/components in the target first if managed is truly required.",
                    CheckName = Name
                });
            }

            return findings;
        }
    }
}
