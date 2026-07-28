using System.Drawing;
using System.Windows.Forms;

namespace SolutionPreflight
{
    /// <summary>
    /// Single place for the plugin's color palette, fonts, and small styling helpers so every tab
    /// looks consistent without repeating the same WinForms boilerplate everywhere.
    /// </summary>
    internal static class UiTheme
    {
        public static readonly Color Primary = ColorTranslator.FromHtml("#4A90D9");
        public static readonly Color PrimaryDark = ColorTranslator.FromHtml("#112D4E");
        public static readonly Color PrimaryDarker = ColorTranslator.FromHtml("#0C2036");
        public static readonly Color Success = ColorTranslator.FromHtml("#2ECC71");
        public static readonly Color Danger = ColorTranslator.FromHtml("#E74C3C");
        public static readonly Color DangerDark = ColorTranslator.FromHtml("#C0392B");
        public static readonly Color Neutral = ColorTranslator.FromHtml("#8A94A6");

        public static readonly Color PageBackground = ColorTranslator.FromHtml("#F4F6FA");
        public static readonly Color CardBackground = Color.White;
        public static readonly Color BorderColor = ColorTranslator.FromHtml("#E1E5EC");
        public static readonly Color TextPrimary = ColorTranslator.FromHtml("#1F2933");
        public static readonly Color TextSecondary = ColorTranslator.FromHtml("#616E7C");

        public static readonly Color ConsoleBackground = ColorTranslator.FromHtml("#1E1E2A");
        public static readonly Color ConsoleForeground = ColorTranslator.FromHtml("#D7DAE0");

        public static readonly Color BlockerBackground = ColorTranslator.FromHtml("#FDECEA");
        public static readonly Color BlockerText = ColorTranslator.FromHtml("#B42318");
        public static readonly Color WarningBackground = ColorTranslator.FromHtml("#FFF6E5");
        public static readonly Color WarningText = ColorTranslator.FromHtml("#93650B");
        public static readonly Color InfoBackground = ColorTranslator.FromHtml("#EAF2FE");
        public static readonly Color InfoText = ColorTranslator.FromHtml("#1B4E9B");

        public static readonly Font FontHeader = new Font("Segoe UI", 15f, FontStyle.Bold);
        public static readonly Font FontSubHeader = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        public static readonly Font FontSectionTitle = new Font("Segoe UI", 10f, FontStyle.Bold);
        public static readonly Font FontBody = new Font("Segoe UI", 9f, FontStyle.Regular);
        public static readonly Font FontBodyBold = new Font("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font FontMono = new Font("Consolas", 9.5f, FontStyle.Regular);
        public static readonly Font FontButton = new Font("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font FontBadge = new Font("Segoe UI", 9f, FontStyle.Bold);

        public static void StylePrimaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Primary;
            button.ForeColor = Color.White;
            button.Font = FontButton;
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            button.FlatAppearance.MouseOverBackColor = PrimaryDark;
            button.FlatAppearance.MouseDownBackColor = PrimaryDarker;
        }

        public static void StyleSecondaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = BorderColor;
            button.BackColor = Color.White;
            button.ForeColor = TextPrimary;
            button.Font = FontBody;
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            button.FlatAppearance.MouseOverBackColor = PageBackground;
        }

        public static void StyleDangerButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Danger;
            button.ForeColor = Color.White;
            button.Font = FontButton;
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            button.FlatAppearance.MouseOverBackColor = DangerDark;
        }

        public static void StyleComboBox(ComboBox combo)
        {
            combo.FlatStyle = FlatStyle.Flat;
            combo.Font = FontBody;
        }

        public static void StyleGrid(DataGridView grid)
        {
            grid.BorderStyle = BorderStyle.None;
            grid.BackgroundColor = CardBackground;
            grid.GridColor = BorderColor;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.EnableHeadersVisualStyles = false;
            grid.RowHeadersVisible = false;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 34;
            grid.RowTemplate.Height = 30;
            grid.ColumnHeadersDefaultCellStyle.BackColor = PrimaryDark;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = PrimaryDark;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = FontBodyBold;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
            grid.DefaultCellStyle.Font = FontBody;
            grid.DefaultCellStyle.ForeColor = TextPrimary;
            grid.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#D9E8FB");
            grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
            grid.DefaultCellStyle.Padding = new Padding(6, 3, 6, 3);
            grid.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#F7F9FC");
        }

        /// <summary>Small colored "pill" label, e.g. for the findings severity summary badges.</summary>
        public static Label CreateBadge(string text, Color background, Color foreground)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = FontBadge,
                BackColor = background,
                ForeColor = foreground,
                Padding = new Padding(10, 4, 10, 4),
                Margin = new Padding(0, 3, 8, 3)
            };
        }
    }
}
