using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using SolutionPreflight.Models;

namespace SolutionPreflight.Analysis
{
    /// <summary>
    /// Goes one level deeper than <see cref="MissingComponentsCheck"/>: parses customizations.xml
    /// out of the exported solution and confirms every referenced entity/attribute actually exists
    /// in the target with a compatible attribute type - not just that a same-named component was
    /// declared. Missing entities/attributes usually also show up via RetrieveMissingComponentsRequest,
    /// but a type mismatch (e.g. a Picklist vs a Whole Number field with the same name) would not.
    ///
    /// Parsing is defensive: if customizations.xml is missing or has an unexpected shape (schema has
    /// changed between Dataverse versions before), this check degrades to a single Info finding
    /// instead of failing the whole analysis run.
    /// </summary>
    public class MetadataCheck : IPreflightCheck
    {
        public string Name => "Metadata";

        public string Category => "Metadata";

        public IEnumerable<PreflightFinding> Run(PreflightContext context)
        {
            var findings = new List<PreflightFinding>();

            List<(string EntityLogicalName, List<string> AttributePhysicalNames)> entities;
            try
            {
                entities = ParseCustomizations(context.CustomizationFile);
            }
            catch (Exception ex)
            {
                findings.Add(new PreflightFinding(
                    Severity.Info,
                    Category,
                    context.SourceSolution.UniqueName,
                    $"Could not parse customizations.xml for a detailed metadata check: {ex.Message}",
                    "Missing entities/attributes are still covered by the Missing Components check.")
                { CheckName = Name });
                return findings;
            }

            foreach (var entity in entities)
            {
                EntityMetadata targetMetadata;
                try
                {
                    var response = (RetrieveEntityResponse)context.TargetService.Execute(new RetrieveEntityRequest
                    {
                        LogicalName = entity.EntityLogicalName,
                        EntityFilters = EntityFilters.Entity | EntityFilters.Attributes
                    });
                    targetMetadata = response.EntityMetadata;
                }
                catch (Exception)
                {
                    // Already reported as Blocker by MissingComponentsCheck when the entity is a real
                    // dependency; avoid duplicate noise here.
                    continue;
                }

                // Best-effort: source metadata is only used for the type/primary-name comparisons
                // below, so a failure here shouldn't stop the plain existence check further down.
                EntityMetadata sourceMetadata = null;
                try
                {
                    var sourceResponse = (RetrieveEntityResponse)context.SourceService.Execute(new RetrieveEntityRequest
                    {
                        LogicalName = entity.EntityLogicalName,
                        EntityFilters = EntityFilters.Entity | EntityFilters.Attributes
                    });
                    sourceMetadata = sourceResponse.EntityMetadata;
                }
                catch (Exception)
                {
                    // Ignore - see comment above.
                }

                if (sourceMetadata != null &&
                    !string.IsNullOrEmpty(sourceMetadata.PrimaryNameAttribute) &&
                    !string.IsNullOrEmpty(targetMetadata.PrimaryNameAttribute) &&
                    !string.Equals(sourceMetadata.PrimaryNameAttribute, targetMetadata.PrimaryNameAttribute, StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new PreflightFinding
                    {
                        Severity = Severity.Warning,
                        Category = Category,
                        ComponentName = entity.EntityLogicalName,
                        ComponentType = "Entity",
                        Message = $"Entity '{entity.EntityLogicalName}' has a different primary name attribute in the source " +
                                  $"('{sourceMetadata.PrimaryNameAttribute}') than in the target ('{targetMetadata.PrimaryNameAttribute}').",
                        SuggestedFix = "Align the primary name attribute between environments before importing - a mismatch here " +
                                       "has been known to make the import fail outright.",
                        CheckName = Name
                    });
                }

                if (sourceMetadata != null && !string.IsNullOrEmpty(sourceMetadata.PrimaryNameAttribute) &&
                    !entity.AttributePhysicalNames.Any(a => string.Equals(a, sourceMetadata.PrimaryNameAttribute, StringComparison.OrdinalIgnoreCase)))
                {
                    findings.Add(new PreflightFinding
                    {
                        Severity = Severity.Warning,
                        Category = Category,
                        ComponentName = entity.EntityLogicalName,
                        ComponentType = "Entity",
                        Message = $"Entity '{entity.EntityLogicalName}' is included in this solution, but its primary name " +
                                  $"attribute ('{sourceMetadata.PrimaryNameAttribute}') isn't among the exported attributes. " +
                                  "Dataverse can reject the import with \"PrimaryName attribute not found\".",
                        SuggestedFix = "In the source, remove and re-add this table (with all its assets) to the solution so the " +
                                       "primary name attribute is captured, then re-export.",
                        CheckName = Name
                    });
                }

                if (sourceMetadata?.Keys != null && targetMetadata.Keys != null)
                {
                    foreach (var sourceKey in sourceMetadata.Keys)
                    {
                        var sourceKeyAttributes = new HashSet<string>(sourceKey.KeyAttributes ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                        if (sourceKeyAttributes.Count == 0)
                        {
                            continue;
                        }

                        foreach (var targetKey in targetMetadata.Keys)
                        {
                            var targetKeyAttributes = new HashSet<string>(targetKey.KeyAttributes ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                            var sameAttributes = sourceKeyAttributes.SetEquals(targetKeyAttributes);
                            var sameKeyName = string.Equals(sourceKey.LogicalName, targetKey.LogicalName, StringComparison.OrdinalIgnoreCase);

                            if (sameAttributes && !sameKeyName)
                            {
                                findings.Add(new PreflightFinding
                                {
                                    Severity = Severity.Blocker,
                                    Category = Category,
                                    ComponentName = $"{entity.EntityLogicalName}.{sourceKey.LogicalName}",
                                    ComponentType = "Entity Key",
                                    Message = $"Entity key '{sourceKey.LogicalName}' on '{entity.EntityLogicalName}' uses the same " +
                                              $"attribute(s) as the target's existing key '{targetKey.LogicalName}'. Dataverse rejects " +
                                              "an entity key whose attribute set duplicates one already present.",
                                    SuggestedFix = "Remove or rename the conflicting key in the target, or align the key names between " +
                                                   "environments before importing.",
                                    CheckName = Name
                                });
                            }
                        }
                    }
                }

                var targetAttributes = targetMetadata.Attributes
                    .Where(a => !string.IsNullOrEmpty(a.LogicalName))
                    .ToDictionary(a => a.LogicalName, a => a, StringComparer.OrdinalIgnoreCase);

                var sourceAttributes = sourceMetadata?.Attributes
                    .Where(a => !string.IsNullOrEmpty(a.LogicalName))
                    .ToDictionary(a => a.LogicalName, a => a, StringComparer.OrdinalIgnoreCase);

                foreach (var physicalName in entity.AttributePhysicalNames)
                {
                    var logicalName = physicalName.ToLowerInvariant();
                    if (!targetAttributes.TryGetValue(logicalName, out var targetAttribute))
                    {
                        findings.Add(new PreflightFinding
                        {
                            Severity = Severity.Warning,
                            Category = Category,
                            ComponentName = $"{entity.EntityLogicalName}.{logicalName}",
                            ComponentType = "Attribute",
                            Message = $"Attribute '{logicalName}' on entity '{entity.EntityLogicalName}' is referenced by the " +
                                      "solution's customizations but was not found in the target's entity metadata.",
                            SuggestedFix = "Verify this attribute is included in the solution or already exists in the target.",
                            CheckName = Name
                        });
                        continue;
                    }

                    if (sourceAttributes != null && sourceAttributes.TryGetValue(logicalName, out var sourceAttribute) &&
                        sourceAttribute.AttributeType.HasValue && targetAttribute.AttributeType.HasValue &&
                        sourceAttribute.AttributeType.Value != targetAttribute.AttributeType.Value)
                    {
                        findings.Add(new PreflightFinding
                        {
                            Severity = Severity.Blocker,
                            Category = Category,
                            ComponentName = $"{entity.EntityLogicalName}.{logicalName}",
                            ComponentType = "Attribute",
                            Message = $"Attribute '{logicalName}' on '{entity.EntityLogicalName}' is a {sourceAttribute.AttributeType} " +
                                      $"in the source but a {targetAttribute.AttributeType} in the target.",
                            SuggestedFix = "Align the attribute type between source and target before importing - Dataverse rejects " +
                                           "a type change on an existing field (e.g. Customer vs. Lookup).",
                            CheckName = Name
                        });
                    }
                }
            }

            return findings;
        }

        private static List<(string EntityLogicalName, List<string> AttributePhysicalNames)> ParseCustomizations(byte[] customizationFile)
        {
            var result = new List<(string, List<string>)>();
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
                    var entityNodes = doc.Descendants("Entity");

                    foreach (var entityNode in entityNodes)
                    {
                        var entityInfo = entityNode.Element("EntityInfo")?.Element("entity");
                        var logicalName = entityInfo?.Attribute("Name")?.Value;
                        if (string.IsNullOrEmpty(logicalName))
                        {
                            continue;
                        }

                        var attributeNames = entityInfo
                            .Element("attributes")
                            ?.Elements("attribute")
                            .Select(a => a.Attribute("PhysicalName")?.Value)
                            .Where(n => !string.IsNullOrEmpty(n))
                            .ToList() ?? new List<string>();

                        result.Add((logicalName, attributeNames));
                    }
                }
            }

            return result;
        }
    }
}
