# Bekannte Import-Fehlerbilder – Übersicht & Tool-Abdeckung

Diese Liste basiert auf real aufgetretenen Dataverse-Import-/Uninstall-Fehlern (mit Fehlercode,
Ursache und Lösung) und zeigt, welche davon Solution Preflight automatisch erkennt – und warum die
übrigen bewusst *nicht* automatisiert wurden.

| # | Fehlercode | Fehlerbild (kurz) | Erkannt? | Check |
|---|---|---|---|---|
| 1 | 8004F020 | RibbonCustomization – blockiert durch andere managed Layer | ✅ | Solution Layers |
| 2 | 8004F020 | EntityRelationship – gleiches Fehlerbild | ✅ | Solution Layers |
| 3 | 8004F020 | EntityMap / AttributeMap – gleiches Fehlerbild | ✅ | Solution Layers |
| 4 | 8004F020 | CustomAPIResponseProperty – gleiches Fehlerbild | ✅ | Solution Layers |
| 5 | 80047009 | SLA-Systemfeld, weil SLA im Ziel deaktiviert ist | ❌ | – (siehe unten) |
| 6 | 80095005 | Connection Reference fehlt bei Flow-Publish | ✅ | Connection References + Cloud Flow Activation |
| 7 | 80040227 | WebResource/Theme Cascade-Restrict | ❌ | – (siehe unten) |
| 8 | 8004801E | DB-Constraint-Kollision (transient) | ⚠️ | nicht prüfbar (Plattform-/Infrastrukturfehler) |
| 9 | 8004801E | SQL "severe error" (transient) | ⚠️ | nicht prüfbar |
| 10 | 80097376 | Flow-Server "BadGateway" (transient) | ⚠️ | nicht prüfbar |
| 11 | — | Transport-Abbruch (transient) | ⚠️ | nicht prüfbar |
| 12 | 80048071 | Plugin-Assembly-Versionssprung entfernt Plugin-Typen | ✅ | Plugin Assembly Versions |
| 13 | 80041A06 | Feldtyp-Konflikt (Customer vs. Lookup) | ✅ | Metadata |
| 14 | — (Import) | Primary-Name-Attribute-Konflikt zwischen Umgebungen | ✅ | Metadata |
| 15 | — (Import) | EntityRelationship existiert im Ziel bereits anders | ❌ | – (siehe unten) |
| 16 | — (Import) | Rollup-Feld zwischen Umgebungen inkonsistent | ❌ | – (siehe unten) |
| 17 | — (Uninstall) | Aktiver Workflow/BPF-Layer blockiert Löschung | ✅ (teilweise) | Solution Layers (Check + manuelles Entfernen im Tab) |
| 18 | — (Uninstall) | "Solution dependencies exist, cannot uninstall" | ✅ (teilweise) | Solution Layers (Check + manuelles Entfernen im Tab) |

## Warum #5, #7, #15, #16 nicht automatisiert sind

Das sind reale, aber sehr spezifische Randfälle, bei denen eine automatische Prüfung entweder tief
in produktinterne Zustände schauen müsste, die nicht zuverlässig über die Standard-APIs abfragbar
sind (z. B. ob SLA als Feature aktiviert ist), oder bei denen die Erkennung nur für den exakt
beschriebenen Fall stimmen würde und in anderen Fällen falsch läge (geraten statt geprüft). Lieber
kein Check als ein Check, der falsche Sicherheit vorgaukelt.

## Warum #8–11 nicht automatisierbar sind

Das sind reine Plattform-/Netzwerk-Aussetzer (Datenbank-Race-Conditions, Gateway-Fehler,
Verbindungsabbrüche) – die haben nichts mit dem Inhalt der Solution zu tun und lassen sich durch
keine Vorab-Analyse vorhersagen. Hier hilft nur: nochmal versuchen.

## Zum Connection-Reference-Beispiel (Con12/Flow1)

Das Szenario "Flow bleibt in der Solution, die dazugehörige Connection Reference wird aber aus der
Solution entfernt (nicht gelöscht, nur nicht mehr als Komponente enthalten)" wurde bisher **nicht**
erkannt, weil der Connection-Reference-Check nur das prüft, was tatsächlich als
Solution-Komponente gelistet ist. Der Cloud-Flow-Check liest jetzt zusätzlich direkt aus der
JSON-Definition jedes Flows aus, welche Connection References er *tatsächlich* verwendet, und
prüft **diese** gegen das Ziel – unabhängig davon, ob sie noch Teil der Solution sind. Damit wird
genau der beschriebene Fall jetzt erkannt (als Blocker, wenn im Ziel gar nichts existiert, als
Warning, wenn dort zwar etwas existiert, aber ohne Verbindung).

## Zur Frage "werden Active Layer auf ALLE Solution-Komponenten geprüft?"

Ja. Der Solution-Layers-Check (`Analysis/SolutionLayerCheck.cs`) und die Solution-Layer-Verwaltung
(`Layers/SolutionLayerService.cs`) lesen zuerst **alle** Zeilen aus `solutioncomponent` für die
gewählte Solution – ohne Filter auf einen bestimmten Komponententyp – und prüfen anschließend für
jede einzelne davon, ob im Ziel ein aktiver Layer einer fremden Solution existiert. Das deckt damit
wirklich jeden Komponententyp ab (Entities, Felder, Formulare, Views, Workflows, Web Resources,
Rollen, Ribbon, Relationships, Custom-API-Komponenten, ...), nicht nur eine Teilmenge.
