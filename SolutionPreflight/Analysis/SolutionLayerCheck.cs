using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk.Query;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// If a component this solution touches already has an active *unmanaged* customization on top
    /// of it in the target (from some other, unrelated unmanaged work), a managed update for that
    /// component can import successfully yet not visibly take effect until the unmanaged layer is
    /// removed or reconciled. This check flags components whose current top layer in the target
    /// belongs to a different, unmanaged solution.
    ///
    /// See <see cref="Layers.SolutionLayerService"/> for the companion bulk-removal feature.
    /// </summary>
    public class SolutionLayerCheck : IPreflightCheck
    {
        private const int BatchSize = 500;

        public string Name => "Solution Layers";

        public string Category => "SolutionLayer";

        public IEnumerable<PreflightFinding> Run(PreflightContext context)
        {
            var findings = new List<PreflightFinding>();

            var componentIds = GetSourceComponentIds(context);
            if (componentIds.Count == 0)
            {
                return findings;
            }

            var unmanagedSolutionNames = GetUnmanagedSolutionNames(context);

            foreach (var batch in Batch(componentIds, BatchSize))
            {
                var query = new QueryExpression("msdyn_componentlayer")
                {
                    ColumnSet = new ColumnSet("msdyn_componentid", "msdyn_name", "msdyn_solutionname", "msdyn_publishername")
                };
                query.Criteria.AddCondition("msdyn_componentid", ConditionOperator.In, batch.Cast<object>().ToArray());
                query.Criteria.AddCondition("msdyn_endtime", ConditionOperator.Null);

                var layers = context.TargetService.RetrieveMultiple(query);

                foreach (var layer in layers.Entities)
                {
                    var solutionName = layer.GetAttributeValue<string>("msdyn_solutionname");
                    var componentName = layer.GetAttributeValue<string>("msdyn_name") ?? layer.GetAttributeValue<System.Guid>("msdyn_componentid").ToString();

                    if (string.IsNullOrEmpty(solutionName) ||
                        string.Equals(solutionName, context.SourceSolution.FriendlyName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(solutionName, context.SourceSolution.UniqueName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var ownedByUnmanaged = unmanagedSolutionNames.Contains(solutionName);

                    findings.Add(new PreflightFinding
                    {
                        Severity = ownedByUnmanaged ? Severity.Warning : Severity.Info,
                        Category = Category,
                        ComponentName = componentName,
                        ComponentType = "Solution Component",
                        Message = ownedByUnmanaged
                            ? $"Component '{componentName}' currently has an active unmanaged customization from solution " +
                              $"'{solutionName}' on top of it in the target. A managed update may not visibly apply until this is addressed."
                            : $"Component '{componentName}' currently has its active layer owned by solution '{solutionName}' in the target.",
                        SuggestedFix = ownedByUnmanaged
                            ? "Review and, if appropriate, remove the unmanaged layer using the Solution Layers tab before importing."
                            : "Usually fine for managed-on-managed layering; verify if unexpected.",
                        CheckName = Name
                    });
                }
            }

            return findings;
        }

        private static List<Guid> GetSourceComponentIds(PreflightContext context)
        {
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid")
            };
            query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, context.SourceSolution.SolutionId);

            var components = context.SourceService.RetrieveMultiple(query);
            return components.Entities
                .Select(e => e.GetAttributeValue<Guid>("objectid"))
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();
        }

        private static HashSet<string> GetUnmanagedSolutionNames(PreflightContext context)
        {
            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("uniquename", "friendlyname")
            };
            query.Criteria.AddCondition("ismanaged", ConditionOperator.Equal, false);

            var solutions = context.TargetService.RetrieveMultiple(query);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var solution in solutions.Entities)
            {
                var uniqueName = solution.GetAttributeValue<string>("uniquename");
                var friendlyName = solution.GetAttributeValue<string>("friendlyname");
                if (!string.IsNullOrEmpty(uniqueName)) names.Add(uniqueName);
                if (!string.IsNullOrEmpty(friendlyName)) names.Add(friendlyName);
            }

            return names;
        }

        private static IEnumerable<List<Guid>> Batch(List<Guid> source, int size)
        {
            for (var i = 0; i < source.Count; i += size)
            {
                yield return source.GetRange(i, Math.Min(size, source.Count - i));
            }
        }
    }
}
