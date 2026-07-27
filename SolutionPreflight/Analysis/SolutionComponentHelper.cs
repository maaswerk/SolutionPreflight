using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// Retrieves the entity-record-backed components (connection references, workflows,
    /// web resources, roles, ...) that belong to a solution, joined through `solutioncomponent`.
    ///
    /// Deliberately does not filter by the numeric `solutioncomponent.componenttype` choice value:
    /// those codes are only partially documented and easy to get wrong. Joining directly on the
    /// child entity's primary key is unambiguous (GUIDs are effectively unique) and avoids having
    /// to hardcode a component-type lookup table.
    /// </summary>
    public static class SolutionComponentHelper
    {
        public static EntityCollection GetSolutionLinkedRecords(
            IOrganizationService service,
            Guid solutionId,
            string childEntityLogicalName,
            string childPrimaryKey,
            params string[] columns)
        {
            var query = new QueryExpression(childEntityLogicalName)
            {
                ColumnSet = columns == null || columns.Length == 0 ? new ColumnSet(true) : new ColumnSet(columns)
            };

            var link = query.AddLink("solutioncomponent", childPrimaryKey, "objectid");
            link.LinkCriteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);

            return service.RetrieveMultiple(query);
        }
    }
}
