# Bekannte Import-Fehlerbilder – Übersicht & Tool-Abdeckung

Diese Datei listet reale Dataverse-Import-/Uninstall-Fehler auf zwei Quellen gestützt und zeigt,
ob bzw. wodurch Solution Preflight sie erkennt:

- **Teil 1**: eine Liste real aufgetretener Fehler aus der Praxis (mit Fehlercode, Ursache, Lösung).
- **Teil 2**: eine vollständige Prüfung **aller** Artikel unter Microsoft Learn ›
  [Troubleshoot Microsoft Dataverse › Working with solutions](https://learn.microsoft.com/en-us/troubleshoot/power-platform/dataverse/working-with-solutions/).

## Teil 1 – Praxis-Fehlerliste

| # | Fehlercode | Fehlerbild (kurz) | Erkannt? | Check |
|---|---|---|---|---|
| 1 | 8004F020 | RibbonCustomization – blockiert durch andere managed Layer | ✅ | Solution Layers + Dependent Components |
| 2 | 8004F020 | EntityRelationship – gleiches Fehlerbild | ✅ | Solution Layers + Dependent Components |
| 3 | 8004F020 | EntityMap / AttributeMap – gleiches Fehlerbild | ✅ | Solution Layers + Dependent Components |
| 4 | 8004F020 | CustomAPIResponseProperty – gleiches Fehlerbild | ✅ | Solution Layers + Dependent Components |
| 5 | 80047009 | SLA-Systemfeld, weil SLA im Ziel deaktiviert ist | ❌ | – (siehe Begründung unten) |
| 6 | 80095005 | Connection Reference fehlt bei Flow-Publish | ✅ | Connection References + Cloud Flow Activation |
| 7 | 80040227 | WebResource/Theme Cascade-Restrict | ❌ | – (siehe Begründung unten) |
| 8 | 8004801E | DB-Constraint-Kollision (transient) | ⚠️ | nicht prüfbar (Plattformfehler) |
| 9 | 8004801E | SQL "severe error" (transient) | ⚠️ | nicht prüfbar |
| 10 | 80097376 | Flow-Server "BadGateway" (transient) | ⚠️ | nicht prüfbar |
| 11 | — | Transport-Abbruch (transient) | ⚠️ | nicht prüfbar |
| 12 | 80048071 | Plugin-Assembly-Versionssprung entfernt Plugin-Typen | ✅ | Plugin Assembly Versions |
| 13 | 80041A06 | Feldtyp-Konflikt (Customer vs. Lookup) | ✅ | Metadata |
| 14 | — (Import) | Primary-Name-Attribute-Konflikt zwischen Umgebungen | ✅ | Metadata |
| 15 | — (Import) | EntityRelationship existiert im Ziel bereits anders | ❌ | – (siehe Begründung unten) |
| 16 | — (Import) | Rollup-Feld zwischen Umgebungen inkonsistent | ❌ | – (siehe Begründung unten) |
| 17 | — (Uninstall) | Aktiver Workflow/BPF-Layer blockiert Löschung | ✅ (teilweise) | Solution Layers + Dependent Components |
| 18 | — (Uninstall) | "Solution dependencies exist, cannot uninstall" | ✅ | Solution Layers + Dependent Components |

### Warum #5, #7, #15, #16 nicht automatisiert sind

Sehr spezifische Randfälle, bei denen eine Prüfung entweder produktinterne Zustände abfragen
müsste, die nicht zuverlässig über Standard-APIs erreichbar sind (z. B. ob SLA als Feature aktiv
ist), oder bei denen die Erkennung nur für genau den beschriebenen Fall stimmen würde und sonst
falsch läge. Lieber kein Check als einer, der falsche Sicherheit vorgaukelt.

### Warum #8–11 nicht automatisierbar sind

Reine Plattform-/Netzwerk-Aussetzer – haben nichts mit dem Solution-Inhalt zu tun, keine
Vorab-Analyse kann sie vorhersagen. Hilft nur: nochmal versuchen.

## Teil 2 – Alle Artikel unter "Working with solutions" (Microsoft Learn)

Alle 45 Artikel aus dem offiziellen Troubleshooting-Bereich, einzeln geprüft.

| Artikel | Symptom (kurz) | Erkannt? | Wodurch / Warum nicht |
|---|---|---|---|
| an-error-calculating-dependencies | Fehlende Abhängigkeits-Komponente beim Import | ✅ | Missing Components |
| assembly-version-error-importing-dynamics-365-solution | Plugin-Assembly nutzt zu alte .NET-Framework-Version | ❌ | müsste Assembly-Manifest im Solution-Zip inspizieren – noch nicht umgesetzt |
| cannot-import-solutions-publish-changes | Import/Publish läuft bereits, zweiter Vorgang kollidiert | ❌ | Race Condition zur Laufzeit, nicht vorab prüfbar |
| cannot-uninstall-solution-error-when-deleting-solution | Uninstall blockiert durch andere managed Solution | ✅ | Solution Layers + Dependent Components |
| changes-not-effective-solution-import | Update kommt an, wirkt aber nicht (Layer blockiert) | ✅ | Solution Layers |
| circular-dependencies-between-solutions | Zwei Solutions hängen zirkulär voneinander ab | ✅ NEU | Dependent Components (erkennt gegenseitige Fremd-Abhängigkeiten je Komponente) |
| concurrent-solution-operation-failures | Gleichzeitige Solution-Operationen kollidieren | ❌ | Laufzeit-/Nebenläufigkeitsproblem, nicht vorab prüfbar |
| dataverse-environment-variables | Environment-Variable-Werte scheinbar nicht aktuell | ⚠️ teilweise | Environment Variables prüft fehlenden Wert bereits; Browser-Cache/SharePoint-URL-Kollision nicht erkennbar |
| duplicate-security-roles | Import legt Rolle mit bereits vergebenem Namen doppelt an | ✅ NEU | Security Roles (Namenskonflikt mit abweichender ID) |
| entitykey-selected-attributes-already-exists | Entity Key mit identischer Attributkombination existiert bereits | ✅ NEU | Metadata (Entity Keys) |
| environment-variable-key-vault-auth-error | Key-Vault-Zugriff für Environment Variable schlägt fehl | ❌ | reines Azure-/Key-Vault-RBAC-Problem, nicht über Dataverse-SDK sichtbar |
| error-code-80040203-invalid-argument-error | Verwaiste BPF-EntityRelationship bricht Import ab | ❌ | zu unsicher, "verwaist" vs. gültig zuverlässig zu unterscheiden |
| error-code-80048539-when-importing-solution-patch | Patch-Version niedriger als Ziel-Version | ✅ NEU | Solution Version |
| error-code-8004f042-occurs | Patch-Version identisch zur Ziel-Version | ✅ NEU | Solution Version |
| error-importing-savedquery-in-dynamics-365 | Managed Update kollidiert mit unmanaged View im Ziel | ✅ | Solution Layers |
| error-when-uninstalling-portals-solutions | Power-Pages-Solutions in falscher Reihenfolge deinstalliert | ❌ | portalspezifische Uninstall-Reihenfolge, außerhalb des Scopes |
| error-when-you-import-delete-solution | Importierendem Benutzer fehlt Berechtigung/Lizenz | ❌ | erfordert Identität des importierenden Nutzers + Privileg-Mapping, zu unsicher |
| form-not-appear-after-import | Unverändertes Formular wird beim Import übersprungen | ✅ NEU | Form XML (`unmodified="1"`) |
| full-formxml-expected-to-create-form-error | Diff-Formular für im Ziel nicht existierendes Formular | ❌ | "Diff vs. Full FormXml" im Export nicht zuverlässig erkennbar |
| install-failure-priviledge-not-assigned | Importierender Nutzer hat falschen Access-Modus/fehlende Rechte | ❌ | gleiche Begründung wie oben |
| invalidpluginassemblycontent-error-when-importing-solution | Assembly geändert, Solution-Version aber nicht erhöht | ✅ | Plugin Assembly Versions + Solution Version zusammen |
| label-text-not-displayed | Übersetzung fehlt, weil Sprache im Ziel nicht aktiviert ist | ❌ | müsste Sprachpaket-Inhalte aus dem Export lesen – noch nicht umgesetzt |
| maximum-row-size-exceeds | Neue Felder sprengen SQL-Zeilenlimit (8060 Byte) | ❌ | Byte-genaue SQL-Schätzung zu unsicher, keine Garantie |
| missing-dependency-on-solution-import | Abhängige App/Komponente fehlt im Ziel | ✅ | Missing Components |
| missing-sales-dependency-on-solution-import | Sales-/Erstanbieter-App fehlt oder falsche Version | ❌ | Lizenz-/Entitlement-Status nicht per SDK sichtbar |
| multiple-publisher-shared-component-failure | Cross-Publisher-Layer blockiert Uninstall | ✅ | Solution Layers + Dependent Components |
| my-apps-area-is-missing | Sitemap-Änderung entfernt "My Apps" | ❌ | zu spezifisch/geringe Praxisrelevanz |
| newly-added-components-dont-appear | App-Komponentenliste wechselt von einzeln zu "Alle" | ❌ | zu spezifisch |
| primaryname-attribute-not-found | Primary-Name-Attribut fehlt im Solution-Export | ✅ NEU | Metadata |
| schemaType-mismatch-on-solution-import | Quelle hat höheren Schema-Tier als Ziel | ❌ | benötigtes Metadatenfeld nicht sicher genug verifiziert |
| solution-cannot-be-deleted-due-to-dependencies | Löschung durch Abhängigkeiten blockiert | ✅ | Dependent Components |
| solution-cannot-be-deleted | Gleiches Fehlerbild | ✅ | Dependent Components |
| solution-checker-enforcement-import-issues | Managed Environment blockt Import wegen Solution-Checker-Verstößen | ❌ | erfordert externen Solution-Checker-Service-Aufruf, außerhalb des Scopes |
| the-import-file-is-too-large-upload | Irreführende Fehlermeldung durch Schema-Namens-Kollision (Groß-/Kleinschreibung) | ❌ | Mechanismus nicht mit ausreichender Sicherheit verifiziert |
| the-import-of-solution-failed-error-when-importing-solution | Feldtyp-Konflikt (z. B. Boolean vs. Picklist) | ✅ | Metadata |
| the-record-could-not-be-deleted | Komponente durch Cascade-Restrict-Referenz blockiert (z. B. Theme) | ❌ | zu spezifisch/Theme-Schema nicht verifiziert |
| the-solution-file-is-invalid | Zip-Paket unvollständig (fehlende Kern-Dateien) | ✅ NEU | Solution Package Integrity |
| the-solution-is-installed-a-managed-one | Managed/Unmanaged-Typ passt nicht zum Ziel | ✅ | Managed / Unmanaged |
| this-solution-package-cannot-be-imported | customizations.xml verletzt XML-Schema | ❌ | volle XSD-Validierung noch nicht umgesetzt |
| troubleshoot-solution-import-errors-in-dynamics-365 | Allgemeiner Troubleshooting-Leitfaden | ❌ | kein konkreter, prüfbarer Einzelfall |
| unable-import-dynamics-365-solution | Datei-Upload-Limit im Ziel zu niedrig | ❌ | müsste Web-Resource-Größen aus dem Export extrahieren – noch nicht umgesetzt |
| unmanaged-active-layer-created-after-solution-import | Fehlendes Fallback-Formular erzeugt automatisch unmanaged Layer | ❌ | Fallback-Form-Kennzeichnung per SDK nicht sicher genug verifiziert |
| upgrade-fails-pending-upgrade | Verwaiste `_Upgrade`-Holding-Solution blockiert erneutes Upgrade | ✅ NEU | Solution Version |
| version-mismatch-on-solution-import | Quelle läuft auf neuerem Dataverse-Build als Ziel | ✅ NEU | Platform Version |
| you-cannot-delete-this-form-only-fallback | Letztes Fallback-Formular einer Tabelle würde gelöscht | ❌ | gleiche Unsicherheit wie oben |

### Neu in dieser Runde umgesetzt

- **Solution Version**: Patch-/Update-Version nicht höher als im Ziel installiert; verwaiste
  `<Solution>_Upgrade`-Holding-Solution im Ziel.
- **Platform Version**: Quelle läuft auf neuerem Dataverse-Build als Ziel.
- **Metadata (erweitert)**: Primary-Name-Attribut fehlt im Export; Entity-Key-Attributkombination
  kollidiert mit einem anders benannten Key im Ziel.
- **Form XML**: Formular ist als `unmodified="1"` markiert und wird beim Import daher übersprungen.
- **Solution Package Integrity**: exportiertes Zip-Paket enthält nicht alle Pflichtdateien.
- **Security Roles (erweitert)**: Rollenname existiert im Ziel bereits unter einer anderen Rollen-ID.
- **Dependent Components** (neu, per `RetrieveDependentComponentsRequest`): fragt für **jede**
  Komponente dieser Solution direkt im Ziel nach, ob eine Komponente aus einer *anderen,
  fremden* Solution aktuell davon abhängt (erkannt über das `dependentcomponentbasesolutionid`-Feld
  der zurückgegebenen `dependency`-Datensätze – ohne die numerischen Component-Type-Codes
  interpretieren zu müssen). Das ist exakt das Muster hinter Fehler 8004F020
  ("blocked by other managed layers") und den verschiedenen "solution dependencies exist, cannot
  uninstall/delete"-Fehlern: Eine völlig unabhängige Solution hängt noch an einer Komponente, die
  diese Solution ändern oder entfernen will.

### Bewusst zurückgestellt (nicht geraten)

Ein paar als "Yes"/"Partially" eingestufte Fälle wurden **nicht** umgesetzt, weil die exakte
API-Antwortstruktur oder das genaue XML-/Metadaten-Detail nicht mit ausreichender Sicherheit
verifiziert werden konnte (u. a. Sprachpaket-Prüfung, Web-Resource-Größenschätzung,
Schema-Namens-Kollisionen bei Groß-/Kleinschreibung, Fallback-Formular-Erkennung). Für diese gilt
das gleiche Prinzip wie überall in diesem Tool: lieber kein Check als einer, der auf einer Vermutung
statt auf einer verifizierten API basiert.

## Zum Connection-Reference-Beispiel (Con12/Flow1)

Das Szenario "Flow bleibt in der Solution, die dazugehörige Connection Reference wird aber aus der
Solution entfernt (nicht gelöscht, nur nicht mehr als Komponente enthalten)" wurde erkannt und
behoben: Der Cloud-Flow-Check liest jetzt direkt aus der JSON-Definition jedes Flows aus, welche
Connection References er *tatsächlich* verwendet, und prüft diese gegen das Ziel – unabhängig
davon, ob sie noch Teil der Solution sind.

## Zur Frage "werden Active Layer auf ALLE Solution-Komponenten geprüft?"

Ja. Der Solution-Layers-Check (`Analysis/SolutionLayerCheck.cs`) und die Solution-Layer-Verwaltung
(`Layers/SolutionLayerService.cs`) lesen zuerst **alle** Zeilen aus `solutioncomponent` für die
gewählte Solution – ohne Filter auf einen bestimmten Komponententyp – und prüfen anschließend für
jede einzelne davon, ob im Ziel ein aktiver Layer einer fremden Solution existiert. Das deckt damit
wirklich jeden Komponententyp ab (Entities, Felder, Formulare, Views, Workflows, Web Resources,
Rollen, Ribbon, Relationships, Custom-API-Komponenten, ...), nicht nur eine Teilmenge.
