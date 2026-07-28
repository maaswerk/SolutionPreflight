using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SolutionPreflight.Analysis;
using SolutionPreflight.Export;
using SolutionPreflight.Layers;
using SolutionPreflight.Models;
using SolutionPreflight.Settings;
using XrmToolBox;
using XrmToolBox.Extensibility;

namespace SolutionPreflight
{
    public partial class SolutionPreflightControl : MultipleConnectionsPluginControlBase
    {
        private IOrganizationService _targetService;
        private ConnectionDetail _targetConnectionDetail;

        private List<SolutionSummary> _sourceSolutions = new List<SolutionSummary>();
        private List<PreflightFinding> _findings = new List<PreflightFinding>();
        private List<ComponentLayerInfo> _layers = new List<ComponentLayerInfo>();

        private readonly SolutionLayerService _layerService = new SolutionLayerService();
        private PluginSettings _settings = new PluginSettings();
        private readonly ToolTip _toolTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 400, ReshowDelay = 200 };

        public SolutionPreflightControl()
        {
            BuildUi();
            WireEvents();
            InitGridColumns();
            LoadSettings();
            RefreshFindingsGrid();
            RefreshLayersGrid();
        }

        private void WireEvents()
        {
            btnConnectTarget.Click += (s, e) => AddAdditionalOrganization();
            btnRefreshSolutions.Click += (s, e) => LoadSourceSolutions();
            btnRunAnalysis.Click += (s, e) => RunAnalysis();

            cmbSeverityFilter.SelectedIndexChanged += (s, e) => RefreshFindingsGrid();
            cmbCategoryFilter.SelectedIndexChanged += (s, e) => RefreshFindingsGrid();
            btnExportReport.Click += (s, e) => ExportReport();

            btnRefreshLayers.Click += (s, e) => RefreshLayers();
            btnSelectAllRemovable.Click += (s, e) => SelectAllRemovableLayers();
            btnRemoveSelectedLayers.Click += (s, e) => RemoveSelectedLayers();

            cmbSeverityFilter.Items.AddRange(new object[] { "All", "Blocker", "Warning", "Info" });
            cmbSeverityFilter.SelectedIndex = 0;
            cmbCategoryFilter.Items.Add("All");
            cmbCategoryFilter.SelectedIndex = 0;

            _toolTip.SetToolTip(btnConnectTarget, "Connect to the Dataverse environment you want to import into.");
            _toolTip.SetToolTip(btnRunAnalysis, "Exports the selected solution and runs every preflight check against the target.");
            _toolTip.SetToolTip(btnRemoveSelectedLayers, "Removes the active (unmanaged) customization layer for the checked components in the TARGET environment. Cannot be undone.");
            _toolTip.SetToolTip(btnSelectAllRemovable, "Checks every layer this tool can safely remove (see the Removable column).");
            _toolTip.SetToolTip(rbManaged, "Simulates importing this solution as a managed solution.");
            _toolTip.SetToolTip(rbUnmanaged, "Simulates importing this solution as an unmanaged solution.");
        }

        private void InitGridColumns()
        {
            dgvFindings.Columns.Clear();
            dgvFindings.Columns.Add(new DataGridViewTextBoxColumn { Name = "Severity", DataPropertyName = "Severity", HeaderText = "Severity", FillWeight = 65 });
            dgvFindings.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", DataPropertyName = "Category", HeaderText = "Category", FillWeight = 80 });
            dgvFindings.Columns.Add(new DataGridViewTextBoxColumn { Name = "ComponentName", DataPropertyName = "ComponentName", HeaderText = "Component", FillWeight = 110 });
            dgvFindings.Columns.Add(new DataGridViewTextBoxColumn { Name = "Message", DataPropertyName = "Message", HeaderText = "Message", FillWeight = 220 });
            dgvFindings.Columns.Add(new DataGridViewTextBoxColumn { Name = "SuggestedFix", DataPropertyName = "SuggestedFix", HeaderText = "Suggested Fix", FillWeight = 220 });
            dgvFindings.CellFormatting += DgvFindings_CellFormatting;

            dgvLayers.Columns.Clear();
            dgvLayers.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", DataPropertyName = "Selected", HeaderText = "", FillWeight = 30 });
            dgvLayers.Columns.Add(new DataGridViewTextBoxColumn { Name = "ComponentName", DataPropertyName = "ComponentName", HeaderText = "Component", ReadOnly = true, FillWeight = 120 });
            dgvLayers.Columns.Add(new DataGridViewTextBoxColumn { Name = "ComponentTypeName", DataPropertyName = "ComponentTypeName", HeaderText = "Type", ReadOnly = true, FillWeight = 80 });
            dgvLayers.Columns.Add(new DataGridViewTextBoxColumn { Name = "SolutionName", DataPropertyName = "SolutionName", HeaderText = "Owning Solution", ReadOnly = true, FillWeight = 100 });
            dgvLayers.Columns.Add(new DataGridViewTextBoxColumn { Name = "PublisherName", DataPropertyName = "PublisherName", HeaderText = "Publisher", ReadOnly = true, FillWeight = 90 });
            dgvLayers.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsUnmanaged", DataPropertyName = "IsUnmanaged", HeaderText = "Unmanaged", ReadOnly = true, FillWeight = 60 });
            dgvLayers.Columns.Add(new DataGridViewCheckBoxColumn { Name = "CanRemove", DataPropertyName = "CanRemove", HeaderText = "Removable", ReadOnly = true, FillWeight = 60 });
        }

        private void DgvFindings_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvFindings.Rows.Count <= e.RowIndex || e.RowIndex < 0)
            {
                return;
            }

            if (!(dgvFindings.Rows[e.RowIndex].DataBoundItem is PreflightFinding finding))
            {
                return;
            }

            var row = dgvFindings.Rows[e.RowIndex];
            var columnName = dgvFindings.Columns[e.ColumnIndex].Name;

            System.Drawing.Color background, text;
            string severityIcon;
            switch (finding.Severity)
            {
                case Severity.Blocker:
                    background = UiTheme.BlockerBackground;
                    text = UiTheme.BlockerText;
                    severityIcon = "⛔";
                    break;
                case Severity.Warning:
                    background = UiTheme.WarningBackground;
                    text = UiTheme.WarningText;
                    severityIcon = "⚠";
                    break;
                default:
                    background = UiTheme.InfoBackground;
                    text = UiTheme.InfoText;
                    severityIcon = "ℹ";
                    break;
            }

            row.DefaultCellStyle.BackColor = background;

            if (columnName == "Severity")
            {
                e.Value = $"{severityIcon}  {finding.Severity}";
                e.CellStyle.ForeColor = text;
                e.CellStyle.Font = UiTheme.FontBodyBold;
                e.FormattingApplied = true;
            }
            else if (columnName == "Category")
            {
                e.Value = FormatCategory(finding.Category);
                e.FormattingApplied = true;
            }
        }

        /// <summary>Turns a PascalCase category code ("ConnectionReference") into readable text ("Connection Reference").</summary>
        private static string FormatCategory(string category)
        {
            return string.IsNullOrEmpty(category) ? category : Regex.Replace(category, "(?<!^)([A-Z])", " $1");
        }

        #region Connections

        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail, string actionName, object parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);

            if (actionName == "AdditionalOrganization")
            {
                _targetService = newService;
                _targetConnectionDetail = detail;
                lblTargetStatus.Text = $"Target: {detail?.ConnectionName}";
                lblTargetDot.ForeColor = UiTheme.Success;
                Log($"Connected to target environment: {detail?.ConnectionName}");
            }
            else
            {
                lblSourceStatus.Text = $"Source: {detail?.ConnectionName}";
                lblSourceDot.ForeColor = UiTheme.Success;
                Log($"Connected to source environment: {detail?.ConnectionName}");
                LoadSourceSolutions();
            }

            UpdateRunButtonState();
        }

        protected override void ConnectionDetailsUpdated(NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                _targetService = null;
                _targetConnectionDetail = null;
                lblTargetStatus.Text = "Target: not connected";
                lblTargetDot.ForeColor = UiTheme.Neutral;
                UpdateRunButtonState();
            }
        }

        private void UpdateRunButtonState()
        {
            btnRunAnalysis.Enabled = Service != null && _targetService != null && cmbSolutions.SelectedItem is SolutionSummary;
        }

        #endregion

        #region Solutions

        private void LoadSourceSolutions()
        {
            if (Service == null)
            {
                return;
            }

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading solutions from source...",
                Work = (worker, args) =>
                {
                    var query = new QueryExpression("solution")
                    {
                        ColumnSet = new ColumnSet("solutionid", "uniquename", "friendlyname", "version", "ismanaged")
                    };
                    query.Criteria.AddCondition("isvisible", ConditionOperator.Equal, true);
                    var link = query.AddLink("publisher", "publisherid", "publisherid");
                    link.EntityAlias = "pub";
                    link.Columns = new ColumnSet("uniquename", "customizationprefix");

                    args.Result = Service.RetrieveMultiple(query);
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show(this, $"Could not load solutions: {args.Error.Message}", "Solution Preflight", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var collection = (EntityCollection)args.Result;
                    _sourceSolutions = collection.Entities.Select(e => new SolutionSummary
                    {
                        SolutionId = e.Id,
                        UniqueName = e.GetAttributeValue<string>("uniquename"),
                        FriendlyName = e.GetAttributeValue<string>("friendlyname"),
                        Version = e.GetAttributeValue<string>("version"),
                        IsManaged = e.GetAttributeValue<bool>("ismanaged"),
                        PublisherUniqueName = e.GetAttributeValue<AliasedValue>("pub.uniquename")?.Value as string,
                        PublisherPrefix = e.GetAttributeValue<AliasedValue>("pub.customizationprefix")?.Value as string
                    })
                    .OrderBy(s => s.FriendlyName)
                    .ToList();

                    cmbSolutions.DataSource = _sourceSolutions;

                    if (!string.IsNullOrEmpty(_settings.LastSourceSolutionUniqueName))
                    {
                        var previous = _sourceSolutions.FirstOrDefault(s => s.UniqueName == _settings.LastSourceSolutionUniqueName);
                        if (previous != null)
                        {
                            cmbSolutions.SelectedItem = previous;
                        }
                    }

                    UpdateRunButtonState();
                }
            });
        }

        #endregion

        #region Analysis

        private void RunAnalysis()
        {
            if (!(cmbSolutions.SelectedItem is SolutionSummary solution))
            {
                MessageBox.Show(this, "Select a solution first.", "Solution Preflight", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (Service == null || _targetService == null)
            {
                MessageBox.Show(this, "Connect to both a source and target environment first.", "Solution Preflight", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var importAsManaged = rbManaged.Checked;
            txtProgressLog.Clear();

            _settings.LastSourceSolutionUniqueName = solution.UniqueName;
            _settings.LastImportAsManaged = importAsManaged;
            SaveSettings();

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Running preflight analysis...",
                Work = (worker, args) =>
                {
                    void Report(string message) => worker.ReportProgress(0, message);

                    Report($"Exporting solution '{solution.UniqueName}' from source (unmanaged, for analysis only)...");
                    var customizationFile = SolutionExportService.ExportUnmanaged(Service, solution.UniqueName, Report);

                    var context = new PreflightContext(Service, _targetService, solution, customizationFile, importAsManaged, Report);

                    var checks = new List<IPreflightCheck>
                    {
                        new SolutionPackageIntegrityCheck(),
                        new MissingComponentsCheck(),
                        new PublisherMismatchCheck(),
                        new ManagedUnmanagedCheck(),
                        new SolutionVersionCheck(),
                        new PlatformVersionCheck(),
                        new SolutionLayerCheck(),
                        new DependentComponentsCheck(),
                        new ConnectionReferenceCheck(),
                        new CloudFlowCheck(),
                        new MetadataCheck(),
                        new FormXmlCheck(),
                        new PcfCheck(),
                        new EnvironmentVariableCheck(),
                        new SecurityRoleCheck(),
                        new PluginAssemblyVersionCheck()
                    };

                    var findings = new List<PreflightFinding>();
                    foreach (var check in checks)
                    {
                        Report($"Running check: {check.Name}...");
                        try
                        {
                            findings.AddRange(check.Run(context));
                        }
                        catch (Exception ex)
                        {
                            findings.Add(new PreflightFinding(Severity.Info, check.Category, solution.UniqueName,
                                $"Check '{check.Name}' failed and was skipped: {ex.Message}", null) { CheckName = check.Name });
                        }
                    }

                    Report("Reading active solution layers in target...");
                    List<ComponentLayerInfo> layers;
                    try
                    {
                        layers = _layerService.GetLayers(Service, _targetService, solution.SolutionId);
                    }
                    catch (Exception ex)
                    {
                        layers = new List<ComponentLayerInfo>();
                        Report($"Could not read solution layers: {ex.Message}");
                    }

                    args.Result = (findings, layers);
                },
                ProgressChanged = args =>
                {
                    if (args.UserState is string message)
                    {
                        Log(message);
                    }
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        Log($"Analysis failed: {args.Error.Message}");
                        MessageBox.Show(this, $"Analysis failed: {args.Error.Message}", "Solution Preflight", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var (findings, layers) = ((List<PreflightFinding>, List<ComponentLayerInfo>))args.Result;
                    _findings = findings;
                    _layers = layers;

                    Log($"Analysis complete: {findings.Count} findings, {layers.Count(l => l.IsUnmanaged)} active unmanaged layers found.");

                    PopulateCategoryFilter();
                    RefreshFindingsGrid();
                    RefreshLayersGrid();
                    tabMain.SelectedTab = tabFindings;
                }
            });
        }

        private void PopulateCategoryFilter()
        {
            var selected = cmbCategoryFilter.SelectedItem as string ?? "All";
            cmbCategoryFilter.Items.Clear();
            cmbCategoryFilter.Items.Add("All");
            foreach (var category in _findings.Select(f => f.Category).Distinct().OrderBy(c => c))
            {
                cmbCategoryFilter.Items.Add(category);
            }

            cmbCategoryFilter.SelectedItem = cmbCategoryFilter.Items.Contains(selected) ? selected : "All";
        }

        private void RefreshFindingsGrid()
        {
            IEnumerable<PreflightFinding> filtered = _findings;

            if (cmbSeverityFilter.SelectedItem is string severity && severity != "All" &&
                Enum.TryParse(severity, out Severity parsedSeverity))
            {
                filtered = filtered.Where(f => f.Severity == parsedSeverity);
            }

            if (cmbCategoryFilter.SelectedItem is string category && category != "All")
            {
                filtered = filtered.Where(f => f.Category == category);
            }

            var ordered = filtered.OrderByDescending(f => f.Severity).ThenBy(f => f.Category).ToList();
            dgvFindings.DataSource = ordered;

            lblBadgeBlocker.Text = $"⛔ {_findings.Count(f => f.Severity == Severity.Blocker)} Blockers";
            lblBadgeWarning.Text = $"⚠ {_findings.Count(f => f.Severity == Severity.Warning)} Warnings";
            lblBadgeInfo.Text = $"ℹ {_findings.Count(f => f.Severity == Severity.Info)} Info";

            var hasFindings = _findings.Count > 0;
            dgvFindings.Visible = hasFindings;
            lblFindingsEmpty.Visible = !hasFindings;
        }

        private void ExportReport()
        {
            if (_findings.Count == 0)
            {
                MessageBox.Show(this, "Run an analysis first.", "Solution Preflight", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new SaveFileDialog
            {
                Filter = "Markdown (*.md)|*.md|CSV (*.csv)|*.csv|HTML (*.html)|*.html",
                FileName = $"SolutionPreflight_{(cmbSolutions.SelectedItem as SolutionSummary)?.UniqueName}_{DateTime.Now:yyyyMMdd_HHmm}"
            })
            {
                if (!string.IsNullOrEmpty(_settings.LastReportFolder))
                {
                    dialog.InitialDirectory = _settings.LastReportFolder;
                }

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var format = dialog.FilterIndex == 2 ? ReportFormat.Csv : dialog.FilterIndex == 3 ? ReportFormat.Html : ReportFormat.Markdown;
                var solutionName = (cmbSolutions.SelectedItem as SolutionSummary)?.UniqueName ?? "solution";

                try
                {
                    ReportExporter.Export(_findings, solutionName, format, dialog.FileName);
                    _settings.LastReportFolder = System.IO.Path.GetDirectoryName(dialog.FileName);
                    _settings.LastReportFormat = format.ToString();
                    SaveSettings();
                    Log($"Report exported to {dialog.FileName}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Could not export report: {ex.Message}", "Solution Preflight", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

        #region Layers

        private void RefreshLayers()
        {
            if (!(cmbSolutions.SelectedItem is SolutionSummary solution) || Service == null || _targetService == null)
            {
                MessageBox.Show(this, "Connect to source and target and select a solution first.", "Solution Preflight", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Reading active solution layers...",
                Work = (worker, args) => { args.Result = _layerService.GetLayers(Service, _targetService, solution.SolutionId); },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show(this, $"Could not read solution layers: {args.Error.Message}", "Solution Preflight", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    _layers = (List<ComponentLayerInfo>)args.Result;
                    RefreshLayersGrid();
                }
            });
        }

        private void RefreshLayersGrid()
        {
            dgvLayers.DataSource = null;
            dgvLayers.DataSource = _layers;

            var hasLayers = _layers.Count > 0;
            dgvLayers.Visible = hasLayers;
            lblLayersEmpty.Visible = !hasLayers;
        }

        private void SelectAllRemovableLayers()
        {
            foreach (var layer in _layers.Where(l => l.CanRemove))
            {
                layer.Selected = true;
            }

            dgvLayers.Refresh();
            RefreshLayersGrid();
        }

        private void RemoveSelectedLayers()
        {
            dgvLayers.EndEdit();
            var selected = _layers.Where(l => l.Selected && l.CanRemove).ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(this, "No removable layers selected.", "Solution Preflight", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(this,
                $"This will remove the active (unmanaged) customization layer for {selected.Count} component(s) in the TARGET " +
                "environment. This cannot be undone. Continue?",
                "Remove Active Layers",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Removing active layers...",
                Work = (worker, args) =>
                {
                    args.Result = _layerService.RemoveActiveLayers(_targetService, selected, msg => worker.ReportProgress(0, msg));
                },
                ProgressChanged = args =>
                {
                    if (args.UserState is string message)
                    {
                        Log(message);
                    }
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show(this, $"Removal failed: {args.Error.Message}", "Solution Preflight", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var results = (List<LayerRemovalResult>)args.Result;
                    var succeeded = results.Count(r => r.Succeeded);
                    var failed = results.Where(r => !r.Succeeded).ToList();

                    Log($"Removed {succeeded}/{results.Count} active layers.");
                    foreach (var failure in failed)
                    {
                        Log($"  Failed: {failure.Layer.ComponentName} ({failure.Layer.ComponentTypeName}) - {failure.Error}");
                    }

                    MessageBox.Show(this,
                        failed.Count == 0
                            ? $"Removed {succeeded} active layer(s) successfully."
                            : $"Removed {succeeded} active layer(s). {failed.Count} failed - see the progress log for details.",
                        "Solution Preflight",
                        MessageBoxButtons.OK,
                        failed.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

                    RefreshLayers();
                }
            });
        }

        #endregion

        #region Settings & logging

        private void LoadSettings()
        {
            if (SettingsManager.Instance.TryLoad(GetType(), out PluginSettings settings) && settings != null)
            {
                _settings = settings;
                rbManaged.Checked = _settings.LastImportAsManaged;
                rbUnmanaged.Checked = !_settings.LastImportAsManaged;
            }
        }

        private void SaveSettings()
        {
            SettingsManager.Instance.Save(GetType(), _settings);
        }

        private void Log(string message)
        {
            if (txtProgressLog.InvokeRequired)
            {
                txtProgressLog.Invoke(new Action(() => Log(message)));
                return;
            }

            txtProgressLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }

        #endregion
    }
}
