using System;
using Microsoft.Xrm.Sdk;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// Shared input every <see cref="IPreflightCheck"/> receives. Built once per analysis run.
    /// </summary>
    public class PreflightContext
    {
        public IOrganizationService SourceService { get; }

        public IOrganizationService TargetService { get; }

        public SolutionSummary SourceSolution { get; }

        /// <summary>
        /// Bytes of the unmanaged customization export of <see cref="SourceSolution"/>,
        /// used by checks that need to inspect the solution.xml (e.g. missing components).
        /// </summary>
        public byte[] CustomizationFile { get; }

        /// <summary>
        /// Whether the user intends to import this solution as managed. Drives
        /// <see cref="ManagedUnmanagedCheck"/>; does not change how the analysis itself runs.
        /// </summary>
        public bool ImportAsManaged { get; }

        /// <summary>
        /// Reports free-text progress back to the UI. Safe to call from a worker thread;
        /// the caller (see <c>SolutionPreflightControl</c>) marshals it via WorkAsync's ProgressChanged.
        /// </summary>
        public Action<string> ReportProgress { get; }

        public PreflightContext(
            IOrganizationService sourceService,
            IOrganizationService targetService,
            SolutionSummary sourceSolution,
            byte[] customizationFile,
            bool importAsManaged,
            Action<string> reportProgress)
        {
            SourceService = sourceService ?? throw new ArgumentNullException(nameof(sourceService));
            TargetService = targetService ?? throw new ArgumentNullException(nameof(targetService));
            SourceSolution = sourceSolution ?? throw new ArgumentNullException(nameof(sourceSolution));
            CustomizationFile = customizationFile;
            ImportAsManaged = importAsManaged;
            ReportProgress = reportProgress ?? (_ => { });
        }
    }
}
