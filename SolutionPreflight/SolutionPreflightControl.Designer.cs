using System.Drawing;
using System.Windows.Forms;

namespace SolutionPreflight
{
    public partial class SolutionPreflightControl
    {
        private Panel pnlHeader;
        private Label lblAppTitle;
        private Label lblAppSubtitle;

        private TabControl tabMain;

        private TabPage tabSetup;
        private Panel pnlSetupCard;
        private Label lblSourceDot;
        private Label lblSourceStatus;
        private Label lblTargetDot;
        private Label lblTargetStatus;
        private Button btnConnectTarget;
        private Label lblSolution;
        private ComboBox cmbSolutions;
        private Button btnRefreshSolutions;
        private Label lblImportType;
        private RadioButton rbManaged;
        private RadioButton rbUnmanaged;
        private Button btnRunAnalysis;
        private Label lblRunHint;
        private Label lblProgressTitle;
        private TextBox txtProgressLog;

        private TabPage tabFindings;
        private Panel pnlFindingsToolbar;
        private Label lblSeverityFilter;
        private ComboBox cmbSeverityFilter;
        private Label lblCategoryFilter;
        private ComboBox cmbCategoryFilter;
        private Button btnExportReport;
        private FlowLayoutPanel pnlSummaryBadges;
        private Label lblBadgeBlocker;
        private Label lblBadgeWarning;
        private Label lblBadgeInfo;
        private DataGridView dgvFindings;
        private Label lblFindingsEmpty;

        private TabPage tabLayers;
        private Panel pnlLayersToolbar;
        private Button btnRefreshLayers;
        private Button btnSelectAllRemovable;
        private Button btnRemoveSelectedLayers;
        private Label lblLayersHint;
        private DataGridView dgvLayers;
        private Label lblLayersEmpty;

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            BackColor = UiTheme.PageBackground;
            Font = UiTheme.FontBody;

            BuildHeader();

            tabMain = new TabControl
            {
                Dock = DockStyle.Fill,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(150, 32),
                Padding = new Point(16, 6)
            };
            tabMain.DrawItem += TabMain_DrawItem;
            tabMain.SelectedIndexChanged += (s, e) => tabMain.Invalidate();

            BuildSetupTab();
            BuildFindingsTab();
            BuildLayersTab();

            tabMain.TabPages.Add(tabSetup);
            tabMain.TabPages.Add(tabFindings);
            tabMain.TabPages.Add(tabLayers);

            Controls.Add(tabMain);
            Controls.Add(pnlHeader);
        }

        private void BuildHeader()
        {
            pnlHeader = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = UiTheme.PrimaryDark, Padding = new Padding(16, 0, 16, 0) };

            lblAppTitle = new Label
            {
                Text = "Solution Preflight",
                AutoSize = true,
                Font = UiTheme.FontHeader,
                ForeColor = Color.White,
                Location = new Point(16, 8),
                BackColor = Color.Transparent
            };

            lblAppSubtitle = new Label
            {
                Text = "Validate a Dataverse solution against a target environment before you import it",
                AutoSize = true,
                Font = UiTheme.FontSubHeader,
                ForeColor = ColorTranslator.FromHtml("#B7C6DC"),
                Location = new Point(18, 36),
                BackColor = Color.Transparent
            };

            pnlHeader.Controls.Add(lblAppTitle);
            pnlHeader.Controls.Add(lblAppSubtitle);
        }

        private void TabMain_DrawItem(object sender, DrawItemEventArgs e)
        {
            var tab = tabMain.TabPages[e.Index];
            var selected = e.Index == tabMain.SelectedIndex;

            using (var bg = new SolidBrush(selected ? UiTheme.Primary : UiTheme.PageBackground))
            {
                e.Graphics.FillRectangle(bg, e.Bounds);
            }

            if (!selected)
            {
                using (var pen = new Pen(UiTheme.BorderColor))
                {
                    e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                }
            }

            var textColor = selected ? Color.White : UiTheme.TextSecondary;
            TextRenderer.DrawText(e.Graphics, tab.Text, UiTheme.FontBodyBold, e.Bounds, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void BuildSetupTab()
        {
            tabSetup = new TabPage("Setup / Run") { BackColor = UiTheme.PageBackground };

            pnlSetupCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 200,
                BackColor = UiTheme.CardBackground,
                Padding = new Padding(18, 14, 18, 14),
                Margin = new Padding(12)
            };

            var connectionsRow = new FlowLayoutPanel { Location = new Point(18, 14), AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            lblSourceDot = new Label { Text = "●", AutoSize = true, ForeColor = UiTheme.Neutral, Font = new Font("Segoe UI", 11f), Margin = new Padding(0, 3, 4, 0) };
            lblSourceStatus = new Label { Text = "Source: not connected", AutoSize = true, Font = UiTheme.FontBodyBold, ForeColor = UiTheme.TextPrimary, Margin = new Padding(0, 5, 24, 0) };
            lblTargetDot = new Label { Text = "●", AutoSize = true, ForeColor = UiTheme.Neutral, Font = new Font("Segoe UI", 11f), Margin = new Padding(0, 3, 4, 0) };
            lblTargetStatus = new Label { Text = "Target: not connected", AutoSize = true, Font = UiTheme.FontBodyBold, ForeColor = UiTheme.TextPrimary, Margin = new Padding(0, 5, 16, 0) };
            btnConnectTarget = new Button { Text = "Connect Target...", AutoSize = true, Padding = new Padding(10, 4, 10, 4), Margin = new Padding(0, 1, 0, 0) };
            UiTheme.StyleSecondaryButton(btnConnectTarget);

            connectionsRow.Controls.Add(lblSourceDot);
            connectionsRow.Controls.Add(lblSourceStatus);
            connectionsRow.Controls.Add(lblTargetDot);
            connectionsRow.Controls.Add(lblTargetStatus);
            connectionsRow.Controls.Add(btnConnectTarget);

            var solutionRow = new FlowLayoutPanel { Location = new Point(18, 56), AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            lblSolution = new Label { Text = "Solution (from source):", AutoSize = true, Font = UiTheme.FontBody, Margin = new Padding(0, 6, 8, 0) };
            cmbSolutions = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 340, Margin = new Padding(0, 2, 8, 0) };
            UiTheme.StyleComboBox(cmbSolutions);
            btnRefreshSolutions = new Button { Text = "⟳ Refresh", AutoSize = true, Padding = new Padding(8, 4, 8, 4) };
            UiTheme.StyleSecondaryButton(btnRefreshSolutions);

            solutionRow.Controls.Add(lblSolution);
            solutionRow.Controls.Add(cmbSolutions);
            solutionRow.Controls.Add(btnRefreshSolutions);

            var importRow = new FlowLayoutPanel { Location = new Point(18, 96), AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            lblImportType = new Label { Text = "Import as:", AutoSize = true, Font = UiTheme.FontBody, Margin = new Padding(0, 6, 10, 0) };
            rbManaged = new RadioButton { Text = "Managed", Checked = true, AutoSize = true, Margin = new Padding(0, 4, 16, 0) };
            rbUnmanaged = new RadioButton { Text = "Unmanaged", AutoSize = true, Margin = new Padding(0, 4, 0, 0) };

            importRow.Controls.Add(lblImportType);
            importRow.Controls.Add(rbManaged);
            importRow.Controls.Add(rbUnmanaged);

            btnRunAnalysis = new Button
            {
                Text = "▶  Run Preflight Analysis",
                Location = new Point(18, 134),
                Size = new Size(240, 38),
                Enabled = false
            };
            UiTheme.StylePrimaryButton(btnRunAnalysis);

            lblRunHint = new Label
            {
                Text = "Runs all checks against the target and reads active solution layers.",
                Location = new Point(272, 145),
                AutoSize = true,
                Font = UiTheme.FontSubHeader,
                ForeColor = UiTheme.TextSecondary
            };

            pnlSetupCard.Controls.Add(connectionsRow);
            pnlSetupCard.Controls.Add(solutionRow);
            pnlSetupCard.Controls.Add(importRow);
            pnlSetupCard.Controls.Add(btnRunAnalysis);
            pnlSetupCard.Controls.Add(lblRunHint);

            lblProgressTitle = new Label
            {
                Text = "PROGRESS LOG",
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(18, 8, 0, 0),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = UiTheme.TextSecondary,
                BackColor = UiTheme.PageBackground
            };

            txtProgressLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                BackColor = UiTheme.ConsoleBackground,
                ForeColor = UiTheme.ConsoleForeground,
                Font = UiTheme.FontMono
            };

            var consoleWrapper = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.ConsoleBackground, Padding = new Padding(10) };
            consoleWrapper.Controls.Add(txtProgressLog);

            tabSetup.Controls.Add(consoleWrapper);
            tabSetup.Controls.Add(lblProgressTitle);
            tabSetup.Controls.Add(pnlSetupCard);
        }

        private void BuildFindingsTab()
        {
            tabFindings = new TabPage("Findings") { BackColor = UiTheme.PageBackground };

            pnlFindingsToolbar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = UiTheme.CardBackground };

            var filterRow = new FlowLayoutPanel { Location = new Point(14, 10), AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            lblSeverityFilter = new Label { Text = "Severity:", AutoSize = true, Font = UiTheme.FontBody, Margin = new Padding(0, 7, 4, 0) };
            cmbSeverityFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, Margin = new Padding(0, 3, 20, 0) };
            UiTheme.StyleComboBox(cmbSeverityFilter);
            lblCategoryFilter = new Label { Text = "Category:", AutoSize = true, Font = UiTheme.FontBody, Margin = new Padding(0, 7, 4, 0) };
            cmbCategoryFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170, Margin = new Padding(0, 3, 20, 0) };
            UiTheme.StyleComboBox(cmbCategoryFilter);
            btnExportReport = new Button { Text = "⬇ Export Report...", AutoSize = true, Padding = new Padding(10, 5, 10, 5), Margin = new Padding(0, 1, 0, 0) };
            UiTheme.StyleSecondaryButton(btnExportReport);

            filterRow.Controls.Add(lblSeverityFilter);
            filterRow.Controls.Add(cmbSeverityFilter);
            filterRow.Controls.Add(lblCategoryFilter);
            filterRow.Controls.Add(cmbCategoryFilter);
            filterRow.Controls.Add(btnExportReport);

            pnlSummaryBadges = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(600, 8)
            };
            lblBadgeBlocker = UiTheme.CreateBadge("0 Blockers", UiTheme.BlockerBackground, UiTheme.BlockerText);
            lblBadgeWarning = UiTheme.CreateBadge("0 Warnings", UiTheme.WarningBackground, UiTheme.WarningText);
            lblBadgeInfo = UiTheme.CreateBadge("0 Info", UiTheme.InfoBackground, UiTheme.InfoText);
            pnlSummaryBadges.Controls.Add(lblBadgeBlocker);
            pnlSummaryBadges.Controls.Add(lblBadgeWarning);
            pnlSummaryBadges.Controls.Add(lblBadgeInfo);

            pnlFindingsToolbar.Controls.Add(filterRow);
            pnlFindingsToolbar.Controls.Add(pnlSummaryBadges);

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
            UiTheme.StyleGrid(dgvFindings);

            lblFindingsEmpty = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UiTheme.FontSubHeader,
                ForeColor = UiTheme.TextSecondary,
                BackColor = UiTheme.CardBackground,
                Text = "No findings yet.\nConnect a source and target, pick a solution, and run a preflight analysis.",
                Visible = false
            };

            tabFindings.Controls.Add(dgvFindings);
            tabFindings.Controls.Add(lblFindingsEmpty);
            tabFindings.Controls.Add(pnlFindingsToolbar);
        }

        private void BuildLayersTab()
        {
            tabLayers = new TabPage("Solution Layers") { BackColor = UiTheme.PageBackground };

            pnlLayersToolbar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = UiTheme.CardBackground };

            var row = new FlowLayoutPanel { Location = new Point(14, 9), AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            btnRefreshLayers = new Button { Text = "⟳ Refresh Active Layers", AutoSize = true, Padding = new Padding(10, 5, 10, 5), Margin = new Padding(0, 1, 8, 0) };
            UiTheme.StyleSecondaryButton(btnRefreshLayers);
            btnSelectAllRemovable = new Button { Text = "Select All Removable", AutoSize = true, Padding = new Padding(10, 5, 10, 5), Margin = new Padding(0, 1, 8, 0) };
            UiTheme.StyleSecondaryButton(btnSelectAllRemovable);
            btnRemoveSelectedLayers = new Button { Text = "🗑 Remove Selected...", AutoSize = true, Padding = new Padding(10, 5, 10, 5), Margin = new Padding(0, 1, 20, 0) };
            UiTheme.StyleDangerButton(btnRemoveSelectedLayers);
            lblLayersHint = new Label
            {
                Text = "Only components mappable to a Web API logical name are removable (see the Removable column).",
                AutoSize = true,
                Font = UiTheme.FontSubHeader,
                ForeColor = UiTheme.TextSecondary,
                Margin = new Padding(0, 9, 0, 0)
            };

            row.Controls.Add(btnRefreshLayers);
            row.Controls.Add(btnSelectAllRemovable);
            row.Controls.Add(btnRemoveSelectedLayers);
            row.Controls.Add(lblLayersHint);

            pnlLayersToolbar.Controls.Add(row);

            dgvLayers = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            UiTheme.StyleGrid(dgvLayers);

            lblLayersEmpty = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UiTheme.FontSubHeader,
                ForeColor = UiTheme.TextSecondary,
                BackColor = UiTheme.CardBackground,
                Text = "No active layers loaded yet.\nRun an analysis or refresh, and they will show up here.",
                Visible = false
            };

            tabLayers.Controls.Add(dgvLayers);
            tabLayers.Controls.Add(lblLayersEmpty);
            tabLayers.Controls.Add(pnlLayersToolbar);
        }
    }
}
