using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SolutionPreflight.Models;

namespace SolutionPreflight.Export
{
    public enum ReportFormat
    {
        Markdown,
        Csv,
        Html
    }

    /// <summary>
    /// Renders a finished analysis run to a shareable file so it can be attached to a ticket,
    /// pasted into a PR description, or reviewed offline.
    /// </summary>
    public static class ReportExporter
    {
        public static void Export(IEnumerable<PreflightFinding> findings, string solutionName, ReportFormat format, string filePath)
        {
            var ordered = findings.OrderByDescending(f => f.Severity).ThenBy(f => f.Category).ToList();

            string content;
            switch (format)
            {
                case ReportFormat.Csv:
                    content = ToCsv(ordered);
                    break;
                case ReportFormat.Html:
                    content = ToHtml(ordered, solutionName);
                    break;
                default:
                    content = ToMarkdown(ordered, solutionName);
                    break;
            }

            File.WriteAllText(filePath, content, Encoding.UTF8);
        }

        private static string ToMarkdown(List<PreflightFinding> findings, string solutionName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Solution Preflight Report – {solutionName}");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine();
            sb.AppendLine($"- Blockers: {findings.Count(f => f.Severity == Severity.Blocker)}");
            sb.AppendLine($"- Warnings: {findings.Count(f => f.Severity == Severity.Warning)}");
            sb.AppendLine($"- Info: {findings.Count(f => f.Severity == Severity.Info)}");
            sb.AppendLine();
            sb.AppendLine("| Severity | Category | Component | Message | Suggested Fix |");
            sb.AppendLine("|---|---|---|---|---|");

            foreach (var f in findings)
            {
                sb.AppendLine($"| {f.Severity} | {Escape(f.Category)} | {Escape(f.ComponentName)} | {Escape(f.Message)} | {Escape(f.SuggestedFix)} |");
            }

            return sb.ToString();

            string Escape(string value) => (value ?? string.Empty).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }

        private static string ToCsv(List<PreflightFinding> findings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Severity,Category,Component,ComponentType,Message,SuggestedFix,Check");
            foreach (var f in findings)
            {
                sb.AppendLine(string.Join(",",
                    CsvField(f.Severity.ToString()),
                    CsvField(f.Category),
                    CsvField(f.ComponentName),
                    CsvField(f.ComponentType),
                    CsvField(f.Message),
                    CsvField(f.SuggestedFix),
                    CsvField(f.CheckName)));
            }

            return sb.ToString();

            string CsvField(string value)
            {
                value = value ?? string.Empty;
                if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                {
                    value = "\"" + value.Replace("\"", "\"\"") + "\"";
                }

                return value;
            }
        }

        private static string ToHtml(List<PreflightFinding> findings, string solutionName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\">");
            sb.AppendLine($"<title>Solution Preflight Report - {System.Net.WebUtility.HtmlEncode(solutionName)}</title>");
            sb.AppendLine("<style>" +
                "body{font-family:Segoe UI,Arial,sans-serif;margin:2rem;color:#1b1b1b}" +
                "table{border-collapse:collapse;width:100%}" +
                "th,td{border:1px solid #ccc;padding:6px 10px;text-align:left;vertical-align:top}" +
                "th{background:#f2f2f2}" +
                ".Blocker{background:#fdecea}.Warning{background:#fff8e1}.Info{background:#e8f4fd}" +
                "</style></head><body>");
            sb.AppendLine($"<h1>Solution Preflight Report – {System.Net.WebUtility.HtmlEncode(solutionName)}</h1>");
            sb.AppendLine($"<p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm}</p>");
            sb.AppendLine($"<p>Blockers: {findings.Count(f => f.Severity == Severity.Blocker)} &nbsp; " +
                           $"Warnings: {findings.Count(f => f.Severity == Severity.Warning)} &nbsp; " +
                           $"Info: {findings.Count(f => f.Severity == Severity.Info)}</p>");
            sb.AppendLine("<table><tr><th>Severity</th><th>Category</th><th>Component</th><th>Message</th><th>Suggested Fix</th></tr>");

            foreach (var f in findings)
            {
                sb.AppendLine($"<tr class=\"{f.Severity}\"><td>{f.Severity}</td><td>{Enc(f.Category)}</td><td>{Enc(f.ComponentName)}</td>" +
                              $"<td>{Enc(f.Message)}</td><td>{Enc(f.SuggestedFix)}</td></tr>");
            }

            sb.AppendLine("</table></body></html>");
            return sb.ToString();

            string Enc(string value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }
}
