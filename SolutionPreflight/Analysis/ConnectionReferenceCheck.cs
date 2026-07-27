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
                        Severity = Severity.Blocker,
                        Category = Category,
                        ComponentName = displayName,
                        ComponentType = "Connection Reference",
                        Message = $"Connection reference '{displayName}' ({logicalName}) does not exist in the target " +
                                  "(it will be created empty by the import, without a connection).",
                        SuggestedFix = "After import, open the connection reference in the target and set its connection, " +
                                       "or pre-create/map it before importing so Cloud Flows can be turned on immediately.",
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
    }
}
