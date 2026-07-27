namespace SolutionPreflight.Models
{
    /// <summary>
    /// Impact level of a <see cref="PreflightFinding"/>. Ordered so that
    /// higher-impact findings sort first when sorted descending.
    /// </summary>
    public enum Severity
    {
        Info = 0,
        Warning = 1,
        Blocker = 2
    }
}
