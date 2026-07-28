using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk.Query;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// If a component this solution touches already has an active layer from some other, unrelated
    /// solution in the target, that can bite in two ways: an unmanaged layer can silently hide a
    /// managed update, or - just as commonly seen in practice - a stale/unrelated *managed* solution
    /// holding a base layer on the same component can block a later uninstall/upgrade outright
    /// (Dataverse error 8004F020, "the uninstall operation will delete the base layer for component
    /// '...' ... blocked by other managed layers", seen for components like RibbonCustomization,
    /// EntityRelationship, EntityMap/AttributeMap and CustomAPIResponseProperty). Both cases are
    /// flagged here; only the wording differs since the fix differs slightly.
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
                        // Both cases have been observed in practice to cause real import/uninstall
                        // failures, not just cosmetic layering quirks - worth a Warning either way.
                        Severity = Severity.Warning,
                        Category = Category,
                        ComponentName = componentName,
                        ComponentType = "Solution Component",
                        Message = ownedByUnmanaged
                            ? $"Component '{componentName}' currently has an active unmanaged customization from solution " +
                              $"'{solutionName}' on top of it in the target. A managed update may not visibly apply until this is addressed."
                            : $"Component '{componentName}' currently has its active layer owned by another managed solution " +
                              $"('{solutionName}') in the target. If this solution later needs to remove or replace this " +
                              "component, that other solution can block it (Dataverse error 8004F020, \"blocked by other managed layers\").",
                        SuggestedFix = ownedByUnmanaged
                            ? "Review and, if appropriate, remove the unmanaged layer using the Solution Layers tab before importing."
                            : $"If you run into 8004F020 later, identify and remove/upgrade '{solutionName}' first - it's usually an old, " +
                              "unrelated solution nobody remembers is still holding this component.",
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
