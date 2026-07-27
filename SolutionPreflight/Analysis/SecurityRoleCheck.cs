using System.Collections.Generic;
using Microsoft.Xrm.Sdk.Query;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// Solution import brings security role *definitions* and their privileges, but never carries
    /// over which users or teams are assigned to a role - that membership is environment-specific.
    /// A role that imports successfully but has nobody assigned to it in the target produces a
    /// confusing "everything looks right but users still get access denied" support case.
    /// </summary>
    public class SecurityRoleCheck : IPreflightCheck
    {
        public string Name => "Security Roles";

        public string Category => "SecurityRole";

        public IEnumerable<PreflightFinding> Run(PreflightContext context)
        {
            var findings = new List<PreflightFinding>();

            var roles = SolutionComponentHelper.GetSolutionLinkedRecords(
                context.SourceService,
                context.SourceSolution.SolutionId,
                "role",
                "roleid",
                "roleid", "name");

            foreach (var role in roles.Entities)
            {
                var roleName = role.GetAttributeValue<string>("name");
                if (string.IsNullOrEmpty(roleName))
                {
                    continue;
                }

                var targetRoleQuery = new QueryExpression("role")
                {
                    ColumnSet = new ColumnSet("roleid")
                };
                targetRoleQuery.Criteria.AddCondition("name", ConditionOperator.Equal, roleName);
                var targetRoles = context.TargetService.RetrieveMultiple(targetRoleQuery);

                if (targetRoles.Entities.Count == 0)
                {
                    // Already reported as a Blocker by MissingComponentsCheck if it's a required component.
                    continue;
                }

                var targetRoleId = targetRoles.Entities[0].Id;
                var assignedUserCount = CountAssignedUsers(context.TargetService, targetRoleId);
                var assignedTeamCount = CountAssignedTeams(context.TargetService, targetRoleId);

                if (assignedUserCount == 0 && assignedTeamCount == 0)
                {
                    findings.Add(new PreflightFinding
                    {
                        Severity = Severity.Warning,
                        Category = Category,
                        ComponentName = roleName,
                        ComponentType = "Security Role",
                        Message = $"Security role '{roleName}' exists in the target but has no users or teams assigned to it.",
                        SuggestedFix = "Assign the relevant users/teams to this role in the target after import - " +
                                       "role membership is never carried over by a solution import.",
                        CheckName = Name
                    });
                }
            }

            return findings;
        }

        private static int CountAssignedUsers(Microsoft.Xrm.Sdk.IOrganizationService service, System.Guid roleId)
        {
            var query = new QueryExpression("systemuser")
            {
                ColumnSet = new ColumnSet("systemuserid")
            };
            var link = query.AddLink("systemuserroles", "systemuserid", "systemuserid");
            link.LinkCriteria.AddCondition("roleid", ConditionOperator.Equal, roleId);
            return service.RetrieveMultiple(query).Entities.Count;
        }

        private static int CountAssignedTeams(Microsoft.Xrm.Sdk.IOrganizationService service, System.Guid roleId)
        {
            var query = new QueryExpression("team")
            {
                ColumnSet = new ColumnSet("teamid")
            };
            var link = query.AddLink("teamroles", "teamid", "teamid");
            link.LinkCriteria.AddCondition("roleid", ConditionOperator.Equal, roleId);
            return service.RetrieveMultiple(query).Entities.Count;
        }
    }
}
