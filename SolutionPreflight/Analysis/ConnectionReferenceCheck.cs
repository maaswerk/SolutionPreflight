using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// Connection references travel with a solution but the actual connection (the OAuth-backed
    /// `connection` record) never does. A missing or unbound connection reference is the single
    /// most common reason a Cloud Flow "imports fine" but can't be turned on in the target.
    /// </summary>
    public class ConnectionReferenceCheck : IPreflightCheck
    {
        public string Name => "Connection References";

        public string Category => "ConnectionReference";

        public IEnumerable<PreflightFinding> Run(PreflightContext context)
        {
            var findings = new List<PreflightFinding>();

            var sourceRefs = SolutionComponentHelper.GetSolutionLinkedRecords(
                context.SourceService,
                context.SourceSolution.SolutionId,
                "connectionreference",
                "connectionreferenceid",
                "connectionreferenceid", "connectionreferencedisplayname", "connectionreferencelogicalname");

            if (sourceRefs.Entities.Count == 0)
            {
                return findings;
            }

            // A connection reference that doesn't exist in the target yet is completely normal for a
            // solution that has never been imported there before - Dataverse creates it during import
            // and the import dialog itself prompts you to pick/create the connection for it. That's
            // not a problem to flag loudly. It only becomes worth a real warning once the solution is
            // already installed in the target and a reference that should already be there and mapped
            // has gone missing (e.g. someone deleted it, or a Cloud Flow can't republish - see error
            // 80095005 "Failed to find connection references with logical name(s)").
            var solutionAlreadyInTarget = SolutionExistsInTarget(context);

            foreach (var sourceRef in sourceRefs.Entities)
            {
                var logicalName = sourceRef.GetAttributeValue<string>("connectionreferencelogicalname");
                var displayName = sourceRef.GetAttributeValue<string>("connectionreferencedisplayname") ?? logicalName;

                if (string.IsNullOrEmpty(logicalName))
                {
                    continue;
                }

                var targetQuery = new QueryExpression("connectionreference")
                {
                    ColumnSet = new ColumnSet("connectionreferenceid", "connectionid", "connectionreferencedisplayname")
                };
                targetQuery.Criteria.AddCondition("connectionreferencelogicalname", ConditionOperator.Equal, logicalName);
                var targetMatches = context.TargetService.RetrieveMultiple(targetQuery);

                if (targetMatches.Entities.Count == 0)
                {
                    findings.Add(new PreflightFinding
                    {
                        // First-time import: this is expected, not a defect - the import dialog will
                        // ask you to map it. Already installed before: more likely something is wrong.
                        Severity = solutionAlreadyInTarget ? Severity.Warning : Severity.Info,
                        Category = Category,
                        ComponentName = displayName,
                        ComponentType = "Connection Reference",
                        Message = solutionAlreadyInTarget
                            ? $"Connection reference '{displayName}' ({logicalName}) no longer exists in the target, even " +
                              "though this solution is already installed there. A Cloud Flow using it may fail to publish/activate."
                            : $"Connection reference '{displayName}' ({logicalName}) doesn't exist in the target yet - this is " +
                              "expected for a first-time import. You'll be prompted to select or create its connection during import.",
                        SuggestedFix = "During (or right after) import, open the connection reference in the target and set its " +
                                       "connection so any Cloud Flow using it can be turned on.",
                        CheckName = Name
                    });
                    continue;
                }

                var targetRef = targetMatches.Entities[0];
                var connectionId = targetRef.GetAttributeValue<string>("connectionid");
                if (string.IsNullOrEmpty(connectionId))
                {
                    findings.Add(new PreflightFinding
                    {
                        Severity = Severity.Warning,
                        Category = Category,
                        ComponentName = displayName,
                        ComponentType = "Connection Reference",
                        Message = $"Connection reference '{displayName}' ({logicalName}) exists in the target but has no connection set.",
                        SuggestedFix = "Set a valid connection on this connection reference in the target before enabling any Cloud Flow that uses it.",
                        CheckName = Name
                    });
                }
            }

            return findings;
        }

        private static bool SolutionExistsInTarget(PreflightContext context)
        {
            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("solutionid")
            };
            query.Criteria.AddCondition("uniquename", ConditionOperator.Equal, context.SourceSolution.UniqueName);
            return context.TargetService.RetrieveMultiple(query).Entities.Count > 0;
        }
    }
}
