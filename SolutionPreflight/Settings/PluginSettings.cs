using System;

namespace SolutionPreflight.Settings
{
    /// <summary>
    /// Persisted via XrmToolBox's SettingsManager (stored per-plugin under the user's XrmToolBox
    /// settings folder). Remembers the last solution/options picked so repeat analyses on the same
    /// source/target pair don't require re-selecting everything.
    /// </summary>
    public class PluginSettings
    {
        public string LastSourceSolutionUniqueName { get; set; }

        public bool LastImportAsManaged { get; set; } = true;

        public string LastReportFormat { get; set; } = "Markdown";

        public string LastReportFolder { get; set; }
    }
}
