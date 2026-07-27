# Solution Preflight – XrmToolBox Tool

> Projekt-Brief für Claude Code. Diese Datei als `CLAUDE.md` oder `PLAN.md` ins Repo-Root legen.
> Arbeitsname: **Solution Preflight** (ggf. anpassen: Solution Deployment Analyzer / Import Advisor).

## 1. Ziel

Ein XrmToolBox-Plugin, das eine Solution aus einem **Source-Environment** gegen ein
**Target-Environment** analysiert und *vor dem Import* meldet, was beim Deployment
fehlschlagen oder Probleme machen könnte – inklusive konkreter Lösungsvorschläge.
Zusätzlich: Solution-Layer-Management (Bulk-Removal aktiver Layer) und ein paar
Quality-of-Life-Features.

Zwei Nutzungssäulen:
1. **Preflight-Analyse** (Flaggschiff): Deployment-Risiken erkennen, bevor importiert wird.
2. **Solution-Layer-Management**: aktive Layer in Masse inspizieren und entfernen.

## 2. Tech-Stack (Stand Ende 2025 / Anfang 2026 – bei Umsetzung verifziert)

- **.NET Framework 4.8** Class Library (Pflicht für XrmToolBox-Plugins)
- **XrmToolBoxPackage** NuGet, `1.2025.10.74` (verifiziert) – liefert die Extensibility-Assemblies,
  `Microsoft.CrmSdk.CoreAssemblies`, `MscrmTools.Xrm.Connection`, `Newtonsoft.Json`
- SDK-style `.csproj` (`net48`, `UseWindowsForms=true`, `PackageReference`) statt altem
  packages.config-Stil – schlankeres Setup, funktional identisch
- Microsoft.CrmSdk.* / `ServiceClient` (IOrganizationService) für Dataverse-Zugriff
- WinForms für die UI

## 3. Zentrale Architektur-Entscheidung: Dual-Connection

Das Plugin braucht **zwei** Verbindungen gleichzeitig (Source + Target).
→ Von `MultipleConnectionsPluginControlBase` ableiten (NICHT `PluginControlBase`).
→ Zweite Verbindung über `AddAdditionalOrganization()` / `AdditionalConnectionDetails`.

Das ist das Fundament. Von Anfang an so aufgesetzt.

## 4. Analyse-Engine – was prüfen und womit

### 4.1 Kern-Message
- **`RetrieveMissingComponentsRequest`**: exportierte Source-Solution (Byte-Array)
  gegen Target ausführen → Liste fehlender Komponenten im Ziel. Deckt den Großteil
  der "Import schlägt fehl"-Fälle ab.

### 4.2 Ergänzende, eigene Vergleiche (was die Message NICHT abdeckt)
| Prüfpunkt | Datenquelle / API | Typisches Problem | Check-Klasse |
|-----------|-------------------|--------------------|--------------|
| Solution-Layer / Base-Layer-Konflikte | `msdyn_componentlayer` | aktive unmanaged Layer im Target blockieren managed Update | `SolutionLayerCheck` |
| Publisher-Mismatch | Publisher-Prefix der Solutions vergleichen | Layering greift nicht, doppelte Komponenten | `PublisherMismatchCheck` |
| Connection References | `connectionreference` Entität in Source vs. Target | Cloud Flow lässt sich nicht aktivieren | `ConnectionReferenceCheck` |
| Cloud Flow Activation | `workflow` (category=modernflow), State | Flow importiert, bleibt aber deaktiviert | `CloudFlowCheck` |
| Fehlende Attribute/Entities | Metadaten-Vergleich (`RetrieveMetadataChangesRequest`) | Solution referenziert Feld, das im Target nicht existiert | `MetadataCheck` |
| PCF Controls | `customcontrol` Komponente / Version | PCF-Version/Manifest im Target inkompatibel oder fehlt | `PcfCheck` |
| Environment Variables | `environmentvariabledefinition` / `...value` | fehlender Wert im Target → Runtime-Fehler | `EnvironmentVariableCheck` |
| Managed vs. Unmanaged | Solution-Typ + Layer | falscher Solution-Typ importiert → keine saubere Deinstallation | `ManagedUnmanagedCheck` |
| Security Roles / Teams | referenzierte Rollen | Import ok, aber Runtime-Rechte fehlen | `SecurityRoleCheck` |

### 4.3 Output-Modell
Jeder Fund als strukturiertes `PreflightFinding`:
- `Severity` (Blocker / Warning / Info)
- `Category` (Layer, ConnectionRef, Metadata, PCF, Flow, …)
- `Component` (Name/Typ/ID)
- `Message` (Was ist das Problem)
- `SuggestedFix` (Wie behebt man es – konkret)

## 5. Solution-Layer-Management

- Layer lesen über `msdyn_componentlayer` (nach Solution / Komponententyp filtern)
- Aktive (unmanaged) Layer entfernen: Web-API-Action **`RemoveActiveCustomization`**
  (Parameter `LogicalName` + `Id`) – verifiziert über öffentliche MS-Dokumentation zu
  Component Types, aber **nicht für alle ~200 Solution-Component-Typen offiziell in
  jedem Detail dokumentiert**. Implementiert für die gängigsten Typen (Entity, Attribute,
  Relationship, Form, View/SavedQuery, Workflow, WebResource, Role, SiteMap);
  unbekannte Typen werden als "manuell im Maker Portal entfernen" markiert statt geraten.
- Bulk-Removal über `ExecuteMultipleRequest` mit Batching + Throttling-Handling (429)
- Vor jeder Löschung: Vorschau + Bestätigung (unmanaged Layer kann gewollte
  Änderungen enthalten → nicht blind entfernen)

## 6. Quality-of-Life-Features (Backlog)

- Analyse-Ergebnis als Report exportieren (Markdown / CSV / HTML)
- Findings filtern/gruppieren nach Severity & Category
- Solution-Komponenten-Diff (Source vs. Target) als eigene Ansicht
- Connection-Reference-Mapping-Helfer (Source-Ref → Target-Connection zuordnen)
- Einstellungen persistieren (letzte Environments/Solutions merken)
- Log-/Fortschrittsanzeige pro Analyse-Schritt

## 7. Umsetzung in Phasen

**Phase 0 – Scaffold & Build-Loop** ✅
**Phase 1 – Connections & Auswahl** ✅
**Phase 2 – Analyse-Engine (Kern)** ✅
**Phase 3 – Results-UI** ✅
**Phase 4 – Solution-Layer-Management** ✅
**Phase 5 – QoL & Polish** ✅

## 8. Wichtige Gotchas (XrmToolBox-Spezifika)

- **Kein `async/await` für Server-Calls** – stattdessen `WorkAsync(WorkAsyncInfo)`.
  Ergebnis kommt im `PostWorkCallBack` zurück; UI nur dort anfassen.
- UI-Thread nie direkt aus Worker-Thread aktualisieren.
- Bulk-Operationen: `ExecuteMultipleRequest`, auf **Service Protection Limits**
  (429/Throttling) reagieren, Batchgröße moderat halten.
- Beim Solution-Export/-Import mit großen Solutions auf Timeouts/Größe achten.

## 9. Repo-Struktur

```
/SolutionPreflight
  SolutionPreflight.sln
  /SolutionPreflight
    SolutionPreflight.csproj
    SolutionPreflightPlugin.cs      // IXrmToolBoxPlugin (Metadaten, Factory)
    SolutionPreflightControl.cs     // MultipleConnectionsPluginControlBase, UI
    /Analysis
      IPreflightCheck.cs
      PreflightContext.cs
      MissingComponentsCheck.cs
      SolutionLayerCheck.cs
      PublisherMismatchCheck.cs
      ConnectionReferenceCheck.cs
      CloudFlowCheck.cs
      MetadataCheck.cs
      PcfCheck.cs
      EnvironmentVariableCheck.cs
      ManagedUnmanagedCheck.cs
      SecurityRoleCheck.cs
    /Layers
      SolutionLayerService.cs       // lesen + Bulk-Remove
    /Models
      PreflightFinding.cs
      Severity.cs
      ComponentLayerInfo.cs
      SolutionSummary.cs
    /Export
      ReportExporter.cs
    /Settings
      PluginSettings.cs
  README.md
  CLAUDE.md                         // dieser Plan
```

## 10. Offene Punkte / bei Umsetzung verifiziert bzw. bewusst konservativ gelöst

- **Layer-Entfernung**: `RemoveActiveCustomization` (Web API Action, Parameter
  `LogicalName`/`Id`) ist die vom make.powerapps.com-Portal selbst genutzte Operation.
  Öffentlich nur teilweise dokumentiert – Implementierung nutzt eine explizite
  Whitelist bekannter Component-Types statt einer geratenen Vollständigkeit.
- **PCF-Versions-/Manifest-Vergleich**: über `customcontrol` Solution-Komponenten
  (Name + Version aus dem Control-Manifest) verglichen; ein tiefergehender
  Bytecode-/Manifest-Diff ist außerhalb des Scopes.
- **`RetrieveMissingComponentsRequest`** deckt strukturelle Abhängigkeiten ab, aber
  keine Runtime-Themen (Connection References, Environment Variables, Flow-Aktivierung,
  Security Roles) – dafür existieren die eigenen Checks in 4.2.
- Aktuelle XrmToolBoxPackage-Version zum Zeitpunkt des Repo-Setups: **1.2025.10.74**.
