using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk.Query;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// A major/minor version jump on a plugin assembly that is already registered in the target can
    /// make Dataverse remove existing plugin type registrations outright (error 80048071, "Existing
    /// plug-in types have been removed. Please update major or minor version") - typically seen with
    /// third-party solutions whose vendor expects old registrations to be deactivated/removed first.
    /// This only compares versions; it can't know whether the vendor's upgrade steps were followed.
    /// </summary>
    public class PluginAssemblyVersionCheck : IPreflightCheck
    {
        public string Name => "Plugin Assembly Versions";

        public string Category => "PluginAssembly";

        public IEnumerable<PreflightFinding> Run(PreflightContext context)
        {
            var findings = new List<PreflightFinding>();

            var sourceAssemblies = SolutionComponentHelper.GetSolutionLinkedRecords(
                context.SourceService,
                context.SourceSolution.SolutionId,
                "pluginassembly",
                "pluginassemblyid",
                "pluginassemblyid", "name", "version");

            foreach (var sourceAssembly in sourceAssemblies.Entities)
            {
                var name = sourceAssembly.GetAttributeValue<string>("name");
                var sourceVersionRaw = sourceAssembly.GetAttributeValue<string>("version");
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(sourceVersionRaw))
                {
                    continue;
                }

                var query = new QueryExpression("pluginassembly")
                {
                    ColumnSet = new ColumnSet("version")
                };
                query.Criteria.AddCondition("name", ConditionOperator.Equal, name);
                var targetMatches = context.TargetService.RetrieveMultiple(query);

                if (targetMatches.Entities.Count == 0)
                {
                    continue; // not registered in target yet - nothing to jump from
                }

                var targetVersionRaw = targetMatches.Entities[0].GetAttributeValue<string>("version");
                if (string.IsNullOrEmpty(targetVersionRaw) ||
                    !Version.TryParse(sourceVersionRaw, out var sourceVersion) ||
                    !Version.TryParse(targetVersionRaw, out var targetVersion))
                {
                    continue; // can't reliably compare - don't guess
                }

                if (sourceVersion.Major != targetVersion.Major || sourceVersion.Minor != targetVersion.Minor)
                {
                    findings.Add(new PreflightFinding
                    {
                        Severity = Severity.Warning,
                        Category = Category,
                        ComponentName = name,
                        ComponentType = "Plugin Assembly",
                        Message = $"Plugin assembly '{name}' is changing from version {targetVersion} (target) to " +
                                  $"{sourceVersion} (source) - a major/minor version jump.",
                        SuggestedFix = "Check the vendor's upgrade instructions before importing: Dataverse can remove existing " +
                                       "plugin type registrations on a major/minor version change (error 80048071), so dependent " +
                                       "plugin steps/processes may need to be deactivated or re-registered first.",
                        CheckName = Name
                    });
                }
            }

            return findings;
        }
    }
}
