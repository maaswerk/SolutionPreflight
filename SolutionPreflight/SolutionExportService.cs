using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace SolutionPreflight
{
    /// <summary>
    /// Exports a solution using the asynchronous export job ("ExportSolutionAsync" +
    /// "DownloadSolutionExportData") instead of the classic synchronous <c>ExportSolutionRequest</c>.
    ///
    /// The synchronous export blocks on a single HTTP call until the whole solution package has been
    /// built server-side, which regularly exceeds the ~2 minute default connection timeout for
    /// anything but small solutions - that's the "Exportieren läuft in einen Timeout" problem this
    /// works around. The async job runs as a system job (`asyncoperation`) with no such limit; this
    /// class starts it, polls until it finishes, and then downloads the resulting file.
    ///
    /// Uses plain <see cref="OrganizationRequest"/>/<see cref="ParameterCollection"/> instead of the
    /// strongly-typed SDK request classes so it keeps working regardless of the exact
    /// Microsoft.CrmSdk.CoreAssemblies version referenced by the project - the messages themselves are
    /// a server-side capability, not something tied to the client DLL.
    /// </summary>
    public static class SolutionExportService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan OverallTimeout = TimeSpan.FromMinutes(30);

        private const int AsyncOperationStateCompleted = 3;
        private const int AsyncOperationStatusSucceeded = 30;

        public static byte[] ExportUnmanaged(IOrganizationService service, string solutionUniqueName, Action<string> reportProgress = null)
        {
            reportProgress = reportProgress ?? (_ => { });

            var exportResponse = service.Execute(new OrganizationRequest("ExportSolutionAsync")
            {
                Parameters = new ParameterCollection
                {
                    ["SolutionName"] = solutionUniqueName,
                    ["Managed"] = false
                }
            });

            var asyncOperationId = (Guid)exportResponse.Results["AsyncOperationId"];
            var exportJobId = (Guid)exportResponse.Results["ExportJobId"];

            reportProgress("Export job started - waiting for it to finish (large solutions can take several minutes)...");
            WaitForCompletion(service, asyncOperationId, reportProgress);

            reportProgress("Export finished, downloading the solution file...");
            var downloadResponse = service.Execute(new OrganizationRequest("DownloadSolutionExportData")
            {
                Parameters = new ParameterCollection
                {
                    ["ExportJobId"] = exportJobId
                }
            });

            return (byte[])downloadResponse.Results["ExportSolutionFile"];
        }

        private static void WaitForCompletion(IOrganizationService service, Guid asyncOperationId, Action<string> reportProgress)
        {
            var stopwatch = Stopwatch.StartNew();

            while (true)
            {
                var operation = service.Retrieve("asyncoperation", asyncOperationId, new ColumnSet("statecode", "statuscode"));
                var stateCode = operation.GetAttributeValue<OptionSetValue>("statecode")?.Value;
                var statusCode = operation.GetAttributeValue<OptionSetValue>("statuscode")?.Value;

                if (stateCode == AsyncOperationStateCompleted)
                {
                    if (statusCode == AsyncOperationStatusSucceeded)
                    {
                        return;
                    }

                    throw new InvalidOperationException($"Solution export job did not succeed (status {statusCode}): {DescribeFailure(service, asyncOperationId)}");
                }

                if (stopwatch.Elapsed > OverallTimeout)
                {
                    throw new TimeoutException($"Solution export did not complete within {OverallTimeout.TotalMinutes:0} minutes.");
                }

                reportProgress($"Still exporting... ({(int)stopwatch.Elapsed.TotalSeconds}s elapsed)");
                Thread.Sleep(PollInterval);
            }
        }

        private static string DescribeFailure(IOrganizationService service, Guid asyncOperationId)
        {
            try
            {
                var operation = service.Retrieve("asyncoperation", asyncOperationId, new ColumnSet("friendlymessage", "message"));
                return operation.GetAttributeValue<string>("friendlymessage")
                       ?? operation.GetAttributeValue<string>("message")
                       ?? "no further details available.";
            }
            catch
            {
                return "no further details available.";
            }
        }
    }
}
