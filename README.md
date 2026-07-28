# Solution Preflight

An [XrmToolBox](https://www.xrmtoolbox.com/) plugin that analyzes a Dataverse solution from a
**source** environment against a **target** environment *before* you import it, and reports what is
likely to fail or need manual follow-up - with concrete suggested fixes. It also includes a
Solution Layer Management view to bulk-inspect and remove active (unmanaged) solution layers in
the target.

**Author:** Fabian Maas

## What it checks

Running an analysis exports the selected solution from the source (unmanaged, for inspection only)
and runs it through:

- **Missing Components** - `RetrieveMissingComponentsRequest` against the target; catches most
  "import will fail" dependency problems.
- **Publisher Mismatch** - would this import be rejected because the solution already exists in the
  target under a different publisher?
- **Managed / Unmanaged** - would this import try to change the solution's installed type?
- **Solution Layers** - does an active unmanaged customization from another solution already sit on
  top of a component this solution touches?
- **Connection References** - do the Cloud Flow connection references exist and have a connection
  configured in the target?
- **Cloud Flow Activation** - reminds you which flows will come in switched off and need an owner.
- **Metadata** - do the entities/attributes referenced in customizations.xml actually exist in the
  target?
- **PCF Controls** - flags controls already registered in the target from another solution.
- **Environment Variables** - will a definition have a usable value (default or explicit) in the
  target after import?
- **Security Roles** - does an imported role actually have any users/teams assigned in the target?

Every finding has a `Severity` (Blocker / Warning / Info), a `Category`, the affected component, a
message, and a suggested fix. Results can be filtered by severity/category and exported to
Markdown, CSV, or HTML from the Findings tab.

## Solution Layer Management

The Solution Layers tab lists the currently active layer for every component the selected solution
touches, and lets you bulk-remove active (unmanaged) layers via the `RemoveActiveCustomization`
Web API action - the same operation used by the maker portal's "Remove Active Customization"
command. Only component types this tool can unambiguously map to a Web API logical name are
removable (see the `Removable` column); everything else must be removed manually in the maker
portal. Always review the preview before confirming - removing an active layer is not undoable.

## Architecture

- Built on `MultipleConnectionsPluginControlBase` so the plugin can hold a source and a target
  connection at the same time (`AddAdditionalOrganization()` for the target).
- All Dataverse calls run inside `WorkAsync`, never on the UI thread.
- `Analysis/IPreflightCheck` implementations are self-contained and exception-safe: a failing check
  degrades to an Info finding instead of aborting the whole run.
- `Analysis/SolutionComponentHelper` joins a solution's components to their owning entity records
  through `solutioncomponent` without hardcoding the numeric `componenttype` choice values (those
  are only partially documented) - it joins on the child entity's primary key instead.

## Requirements

- .NET Framework 4.8 SDK / Visual Studio 2022 with the ".NET desktop development" workload
- [XrmToolBox](https://www.xrmtoolbox.com/) installed for local testing

## Building

```
dotnet build SolutionPreflight.sln
```

(or open `SolutionPreflight.sln` in Visual Studio and build normally). NuGet restores
`XrmToolBoxPackage`, which brings in the XrmToolBox extensibility assemblies, the Dataverse SDK
core assemblies, and the connection manager.

## Local debugging

XrmToolBox can load a plugin directly from a build output folder without installing it, via the
`/overridepath` switch:

```
XrmToolBox.exe /overridepath:"C:\path\to\SolutionPreflight\SolutionPreflight\bin\Debug"
```

In Visual Studio, set this as the debug target executable and command-line argument in the
project's Debug launch profile to hit breakpoints directly. Alternatively, build with
`/p:CopyToXtbPlugins=true` to copy the output into
`%AppData%\MscrmTools\XrmToolBox\Plugins\SolutionPreflight` so it shows up in a normal XrmToolBox
install.

## Repository structure

```
/SolutionPreflight
  SolutionPreflight.sln
  /SolutionPreflight
    SolutionPreflight.csproj
    SolutionPreflightPlugin.cs        # IXrmToolBoxPlugin metadata + factory
    SolutionPreflightControl.cs       # MultipleConnectionsPluginControlBase, behavior
    SolutionPreflightControl.Designer.cs  # UI layout
    /Analysis                          # IPreflightCheck + the 9 checks
    /Layers                            # SolutionLayerService (read + bulk-remove active layers)
    /Models                            # PreflightFinding, Severity, ComponentLayerInfo, SolutionSummary
    /Export                            # ReportExporter (Markdown/CSV/HTML)
    /Settings                          # PluginSettings (persisted via XrmToolBox SettingsManager)
    /Resources                         # Plugin icon source PNGs
```
