using System.Drawing;
using System.Windows.Forms;

namespace SolutionPreflight
{
    public partial class SolutionPreflightControl
    {
        private TabControl tabMain;

        private TabPage tabSetup;
        private Label lblSourceStatus;
        private Label lblTargetStatus;
        private Button btnConnectTarget;
        private Label lblSolution;
        private ComboBox cmbSolutions;
        private Button btnRefreshSolutions;
        private GroupBox grpImportType;
        private RadioButton rbManaged;
        private RadioButton rbUnmanaged;
        private Button btnRunAnalysis;
        private TextBox txtProgressLog;

        private TabPage tabFindings;
        private Label lblSeverityFilter;
        private ComboBox cmbSeverityFilter;
        private Label lblCategoryFilter;
        private ComboBox cmbCategoryFilter;
        private Button btnExportReport;
        private Label lblFindingsSummary;
        private DataGridView dgvFindings;

        private TabPage tabLayers;
        private Button btnRefreshLayers;
        private Button btnSelectAllRemovable;
        private Button btnRemoveSelectedLayers;
        private Label lblLayersHint;
        private DataGridView dgvLayers;

        private void BuildUi()
        {
            Dock = DockStyle.Fill;

            tabMain = new TabControl { Dock = DockStyle.Fill };

            BuildSetupTab();
            BuildFindingsTab();
            BuildLayersTab();

            tabMain.TabPages.Add(tabSetup);
            tabMain.TabPages.Add(tabFindings);
            tabMain.TabPages.Add(tabLayers);

            Controls.Add(tabMain);
        }

        private void BuildSetupTab()
        {
            tabSetup = new TabPage("Setup / Run");

            var topPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 190,
                ColumnCount = 3,
                Padding = new Padding(8)
            };
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

            lblSourceStatus = new Label { Text = "Source: not connected", AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            lblTargetStatus = new Label { Text = "Target: not connected", AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            btnConnectTarget = new Button { Text = "Connect to Target Environment...", AutoSize = true };

            lblSolution = new Label { Text = "Solution (from source):", AutoSize = true };
            cmbSolutions = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 350 };
            btnRefreshSolutions = new Button { Text = "Refresh", AutoSize = true };

            grpImportType = new GroupBox { Text = "Intended import type", AutoSize = true, Width = 350, Height = 60 };
            rbManaged = new RadioButton { Text = "Managed", Checked = true, Left = 10, Top = 20, AutoSize = true };
            rbUnmanaged = new RadioButton { Text = "Unmanaged", Left = 120, Top = 20, AutoSize = true };
            grpImportType.Controls.Add(rbManaged);
            grpImportType.Controls.Add(rbUnmanaged);

            btnRunAnalysis = new Button { Text = "Run Preflight Analysis", AutoSize = true, Height = 32, Enabled = false };

            topPanel.RowCount = 4;
            topPanel.Controls.Add(lblSourceStatus, 0, 0);
            topPanel.Controls.Add(lblTargetStatus, 1, 0);
            topPanel.Controls.Add(btnConnectTarget, 2, 0);
            topPanel.Controls.Add(lblSolution, 0, 1);
            topPanel.Controls.Add(cmbSolutions, 1, 1);
            topPanel.Controls.Add(btnRefreshSolutions, 2, 1);
            topPanel.Controls.Add(grpImportType, 0, 2);
            topPanel.Controls.Add(btnRunAnalysis, 1, 3);

            txtProgressLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font(FontFamily.GenericMonospace, 8.5f)
            };

            tabSetup.Controls.Add(txtProgressLog);
            tabSetup.Controls.Add(topPanel);
        }

        private void BuildFindingsTab()
        {
            tabFindings = new TabPage("Findings");

            var topPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8) };
            lblSeverityFilter = new Label { Text = "Severity:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) };
            cmbSeverityFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
            lblCategoryFilter = new Label { Text = "Category:", AutoSize = true, Padding = new Padding(12, 8, 4, 0) };
            cmbCategoryFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
            btnExportReport = new Button { Text = "Export Report...", AutoSize = true, Margin = new Padding(20, 3, 3, 3) };
            lblFindingsSummary = new Label { Text = string.Empty, AutoSize = true, Padding = new Padding(20, 8, 4, 0) };

            topPanel.Controls.Add(lblSeverityFilter);
            topPanel.Controls.Add(cmbSeverityFilter);
            topPanel.Controls.Add(lblCategoryFilter);
            topPanel.Controls.Add(cmbCategoryFilter);
            topPanel.Controls.Add(btnExportReport);
            topPanel.Controls.Add(lblFindingsSummary);

            dgvFindings = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            tabFindings.Controls.Add(dgvFindings);
            tabFindings.Controls.Add(topPanel);
        }

        private void BuildLayersTab()
        {
            tabLayers = new TabPage("Solution Layers");

            var topPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8) };
            btnRefreshLayers = new Button { Text = "Refresh Active Layers", AutoSize = true };
            btnSelectAllRemovable = new Button { Text = "Select All Removable", AutoSize = true, Margin = new Padding(12, 3, 3, 3) };
            btnRemoveSelectedLayers = new Button { Text = "Remove Selected Active Layers...", AutoSize = true, Margin = new Padding(12, 3, 3, 3) };
            lblLayersHint = new Label
            {
                Text = "Only components this tool can safely map to a Web API logical name are removable (see CanRemove column).",
                AutoSize = true,
                Padding = new Padding(20, 8, 4, 0)
            };

            topPanel.Controls.Add(btnRefreshLayers);
            topPanel.Controls.Add(btnSelectAllRemovable);
            topPanel.Controls.Add(btnRemoveSelectedLayers);
            topPanel.Controls.Add(lblLayersHint);

            dgvLayers = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            tabLayers.Controls.Add(dgvLayers);
            tabLayers.Controls.Add(topPanel);
        }
    }
}
