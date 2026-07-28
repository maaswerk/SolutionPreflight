using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// Dataverse rejects any solution package that isn't a valid zip containing solution.xml,
    /// customizations.xml, and [Content_Types].xml at the root (error 80048060, "The solution file
    /// is invalid"). This normally can't happen with a package this tool exported itself, but a
    /// truncated download or an interrupted export could still produce a broken file - and since
    /// the check is essentially free (no target round-trip needed), it's worth confirming.
    /// </summary>
    public class SolutionPackageIntegrityCheck : IPreflightCheck
    {
        private static readonly string[] RequiredEntries = { "solution.xml", "customizations.xml", "[Content_Types].xml" };

        public string Name => "Solution Package Integrity";

        public string Category => "PackageIntegrity";

        public IEnumerable<PreflightFinding> Run(PreflightContext context)
        {
            var findings = new List<PreflightFinding>();

            if (context.CustomizationFile == null || context.CustomizationFile.Length == 0)
            {
                return findings; // already reported by MissingComponentsCheck
            }

            try
            {
                using (var stream = new MemoryStream(context.CustomizationFile))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    foreach (var required in RequiredEntries)
                    {
                        if (archive.GetEntry(required) == null)
                        {
                            findings.Add(new PreflightFinding
                            {
                                Severity = Severity.Blocker,
                                Category = Category,
                                ComponentName = context.SourceSolution.UniqueName,
                                ComponentType = "Solution Package",
                                Message = $"The exported solution package is missing '{required}' - Dataverse rejects an import " +
                                          "whose package doesn't contain solution.xml, customizations.xml, and [Content_Types].xml.",
                                SuggestedFix = "Re-run the export; if this keeps happening, the export itself may be failing partway through.",
                                CheckName = Name
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                findings.Add(new PreflightFinding(
                    Severity.Warning,
                    Category,
                    context.SourceSolution.UniqueName,
                    $"Could not open the exported solution package as a zip archive: {ex.Message}",
                    "Re-run the analysis; if this persists, the export may be corrupted.")
                { CheckName = Name });
            }

            return findings;
        }
    }
}
