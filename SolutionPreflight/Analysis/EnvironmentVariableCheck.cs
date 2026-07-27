using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// Environment variable *definitions* travel with the solution, but their *values* are
    /// environment-specific data and are only carried over if a default value was set - and even
    /// then a Secret-type variable never gets a default. A definition with no usable value in the
    /// target is a classic silent runtime failure (plugin/flow reads an empty string).
    /// </summary>
    public class EnvironmentVariableCheck : IPreflightCheck
    {
        public string Name => "Environment Variables";

        public string Category => "EnvironmentVariable";

        public IEnumerable<PreflightFinding> Run(PreflightContext context)
        {
            var findings = new List<PreflightFinding>();

            var definitions = SolutionComponentHelper.GetSolutionLinkedRecords(
                context.SourceService,
                context.SourceSolution.SolutionId,
                "environmentvariabledefinition",
                "environmentvariabledefinitionid",
                "environmentvariabledefinitionid", "schemaname", "displayname", "defaultvalue", "type");

            foreach (var definition in definitions.Entities)
            {
                var schemaName = definition.GetAttributeValue<string>("schemaname");
                var displayName = definition.GetAttributeValue<string>("displayname") ?? schemaName;
                var defaultValue = definition.GetAttributeValue<string>("defaultvalue");
                var typeLabel = definition.GetAttributeValue<OptionSetValue>("type")?.Value;
                var isSecret = typeLabel == 100000005; // "Secret" choice value, only used for a friendlier message below

                if (string.IsNullOrEmpty(schemaName))
                {
                    continue;
                }

                var sourceHasValue = HasExplicitValue(context.SourceService, definition.Id);

                var targetDefQuery = new QueryExpression("environmentvariabledefinition")
                {
                    ColumnSet = new ColumnSet("environmentvariabledefinitionid", "defaultvalue")
                };
                targetDefQuery.Criteria.AddCondition("schemaname", ConditionOperator.Equal, schemaName);
                var targetDefs = context.TargetService.RetrieveMultiple(targetDefQuery);

                bool targetHasUsableValue;
                if (targetDefs.Entities.Count == 0)
                {
                    // Will be created fresh by the import; only the default value (if any) will exist.
                    targetHasUsableValue = !isSecret && !string.IsNullOrEmpty(defaultValue);
                }
                else
                {
                    var targetDef = targetDefs.Entities[0];
                    var targetDefaultValue = targetDef.GetAttributeValue<string>("defaultvalue");
                    var targetHasExplicitValue = HasExplicitValue(context.TargetService, targetDef.Id);
                    targetHasUsableValue = targetHasExplicitValue || (!isSecret && !string.IsNullOrEmpty(targetDefaultValue));
                }

                if (!targetHasUsableValue)
                {
                    findings.Add(new PreflightFinding
                    {
                        Severity = sourceHasValue ? Severity.Warning : Severity.Info,
                        Category = Category,
                        ComponentName = displayName,
                        ComponentType = "Environment Variable",
                        Message = isSecret
                            ? $"Environment variable '{displayName}' is a Secret and will have no value in the target after import."
                            : $"Environment variable '{displayName}' has no default value and no value will exist in the target after import.",
                        SuggestedFix = $"Set an explicit value for '{schemaName}' in the target environment after import.",
                        CheckName = Name
                    });
                }
            }

            return findings;
        }

        private static bool HasExplicitValue(IOrganizationService service, System.Guid definitionId)
        {
            var query = new QueryExpression("environmentvariablevalue")
            {
                ColumnSet = new ColumnSet("value")
            };
            query.Criteria.AddCondition("environmentvariabledefinitionid", ConditionOperator.Equal, definitionId);
            var values = service.RetrieveMultiple(query);
            foreach (var value in values.Entities)
            {
                if (!string.IsNullOrEmpty(value.GetAttributeValue<string>("value")))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
