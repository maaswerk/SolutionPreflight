using System.Collections.Generic;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// A single, self-contained deployment risk check. Implementations must not throw for
    /// expected data issues (missing records, etc.) - report an Info/Warning finding instead so
    /// one failing check never aborts the whole run.
    /// </summary>
    public interface IPreflightCheck
    {
        string Name { get; }

        string Category { get; }

        IEnumerable<PreflightFinding> Run(PreflightContext context);
    }
}
