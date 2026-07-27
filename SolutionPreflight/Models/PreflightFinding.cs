using System;

namespace SolutionPreflight.Models
{
    /// <summary>
    /// A single result produced by an <see cref="Analysis.IPreflightCheck"/>.
    /// </summary>
    public class PreflightFinding
    {
        public Severity Severity { get; set; }

        public string Category { get; set; }

        public string ComponentName { get; set; }

        public string ComponentType { get; set; }

        public Guid? ComponentId { get; set; }

        public string Message { get; set; }

        public string SuggestedFix { get; set; }

        /// <summary>
        /// Name of the check that produced this finding, kept for traceability in exports.
        /// </summary>
        public string CheckName { get; set; }

        public PreflightFinding()
        {
        }

        public PreflightFinding(Severity severity, string category, string componentName, string message, string suggestedFix)
        {
            Severity = severity;
            Category = category;
            ComponentName = componentName;
            Message = message;
            SuggestedFix = suggestedFix;
        }
    }
}
