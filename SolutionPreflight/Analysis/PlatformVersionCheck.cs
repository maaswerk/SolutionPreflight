using System;
using System.Collections.Generic;
using Microsoft.Crm.Sdk.Messages;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// A solution exported from an environment running a newer Dataverse platform build than the
    /// target can import with "missing dependency"-style errors, because the source may already have
    /// component types/behaviors the target's older build doesn't understand yet. This is exactly
    /// the check Dataverse's own import dialog performs and warns about.
    /// </summary>
    public class PlatformVersionCheck : IPreflightCheck
    {
        public string Name => "Platform Version";

        public string Category => "PlatformVersion";

        public IEnumerable<PreflightFinding> Run(PreflightContext context)
        {
            var findings = new List<PreflightFinding>();

            string sourceVersionRaw;
            string targetVersionRaw;

            try
            {
                sourceVersionRaw = ((RetrieveVersionResponse)context.SourceService.Execute(new RetrieveVersionRequest())).Version;
                targetVersionRaw = ((RetrieveVersionResponse)context.TargetService.Execute(new RetrieveVersionRequest())).Version;
            }
            catch (Exception)
            {
                return findings; // not critical enough to fail the run over
            }

            if (Version.TryParse(sourceVersionRaw, out var sourceVersion) &&
                Version.TryParse(targetVersionRaw, out var targetVersion) &&
                sourceVersion > targetVersion)
            {
                findings.Add(new PreflightFinding
                {
                    Severity = Severity.Warning,
                    Category = Category,
                    ComponentName = context.SourceSolution.UniqueName,
                    ComponentType = "Environment",
                    Message = $"The source environment is running a newer Dataverse platform build ({sourceVersion}) than the " +
                              $"target ({targetVersion}). Importing from a newer build into an older one can cause missing-dependency " +
                              "or unrecognized-component errors.",
                    SuggestedFix = "If possible, use a source environment whose platform build is the same as or older than the target.",
                    CheckName = Name
                });
            }

            return findings;
        }
    }
}
