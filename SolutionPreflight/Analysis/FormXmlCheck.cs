using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// A form exported while unmodified from its original definition is marked
    /// <c>unmodified="1"</c> in customizations.xml, and Dataverse deliberately skips applying such
    /// forms on import - so it silently never reaches the target, even though the import itself
    /// succeeds. Parsing is defensive: an unexpected schema shape degrades to "found nothing"
    /// rather than failing the whole analysis run.
    /// </summary>
    public class FormXmlCheck : IPreflightCheck
    {
        public string Name => "Form XML";

        public string Category => "Form";

        public IEnumerable<PreflightFinding> Run(PreflightContext context)
        {
            var findings = new List<PreflightFinding>();

            List<(string EntityLogicalName, string FormName)> unmodifiedForms;
            try
            {
                unmodifiedForms = FindUnmodifiedForms(context.CustomizationFile);
            }
            catch (Exception)
            {
                return findings; // degrade silently - this check is a bonus, not load-bearing
            }

            foreach (var form in unmodifiedForms)
            {
                findings.Add(new PreflightFinding
                {
                    Severity = Severity.Warning,
                    Category = Category,
                    ComponentName = string.IsNullOrEmpty(form.FormName) ? form.EntityLogicalName : $"{form.EntityLogicalName}.{form.FormName}",
                    ComponentType = "Form",
                    Message = $"Form '{form.FormName}' on entity '{form.EntityLogicalName}' is marked as unmodified in the " +
                              "export. Dataverse skips applying unmodified forms on import, so this form will not actually be " +
                              "updated in the target even though the import succeeds.",
                    SuggestedFix = "If this form is supposed to reach the target, make a small change to it in the source before " +
                                   "exporting (even reordering a field and undoing it is enough to clear the \"unmodified\" marker).",
                    CheckName = Name
                });
            }

            return findings;
        }

        private static List<(string, string)> FindUnmodifiedForms(byte[] customizationFile)
        {
            var result = new List<(string, string)>();
            if (customizationFile == null || customizationFile.Length == 0)
            {
                return result;
            }

            using (var stream = new MemoryStream(customizationFile))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var entry = archive.GetEntry("customizations.xml");
                if (entry == null)
                {
                    return result;
                }

                using (var entryStream = entry.Open())
                {
                    var doc = XDocument.Load(entryStream);

                    foreach (var entityNode in doc.Descendants("Entity"))
                    {
                        var entityLogicalName = entityNode.Element("Name")?.Value
                                                 ?? entityNode.Element("EntityInfo")?.Element("entity")?.Attribute("Name")?.Value
                                                 ?? "(unknown entity)";

                        var formXml = entityNode.Element("FormXml");
                        if (formXml == null)
                        {
                            continue;
                        }

                        foreach (var systemForm in formXml.Descendants("systemform"))
                        {
                            var unmodifiedValue = systemForm.Attribute("unmodified")?.Value ?? systemForm.Element("unmodified")?.Value;
                            var isUnmodified = !string.IsNullOrEmpty(unmodifiedValue) &&
                                                (unmodifiedValue == "1" || string.Equals(unmodifiedValue, "true", StringComparison.OrdinalIgnoreCase));

                            if (!isUnmodified)
                            {
                                continue;
                            }

                            var formName = systemForm.Element("name")?.Value ?? systemForm.Attribute("Name")?.Value ?? "(unnamed form)";
                            result.Add((entityLogicalName, formName));
                        }
                    }
                }
            }

            return result;
        }
    }
}
