using System;

namespace SolutionPreflight.Models
{
    /// <summary>
    /// Lightweight projection of a `solution` record used to populate the solution picker
    /// and to carry the exported customization file through the analysis pipeline.
    /// </summary>
    public class SolutionSummary
    {
        public Guid SolutionId { get; set; }

        public string UniqueName { get; set; }

        public string FriendlyName { get; set; }

        public string Version { get; set; }

        public bool IsManaged { get; set; }

        public string PublisherUniqueName { get; set; }

        public string PublisherPrefix { get; set; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(FriendlyName) ? UniqueName : $"{FriendlyName} ({UniqueName}) v{Version}";
        }
    }
}
