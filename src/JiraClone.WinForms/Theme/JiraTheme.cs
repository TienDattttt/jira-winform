using System.Drawing;
using System.Windows.Forms;

namespace JiraClone.WinForms.Theme;

public static class JiraTheme
{
    public static Color Blue500 => FromHex("#4688EC");
    public static Color Blue600 => FromHex("#357DE8");
    public static Color Blue700 => FromHex("#1868DB");
    public static Color Blue100 => FromHex("#E9F2FE");
    public static Color Neutral0 => FromHex("#FFFFFF");
    public static Color Neutral100 => FromHex("#F8F8F8");
    public static Color Neutral200 => FromHex("#F0F1F2");
    public static Color Neutral300 => FromHex("#DDDEE1");
    public static Color Neutral500 => FromHex("#8C8F97");
    public static Color Neutral700 => FromHex("#6B6E76");
    public static Color Neutral1100 => FromHex("#1E1F21");
    public static Color Red500 => FromHex("#F15B50");
    public static Color Red700 => FromHex("#C9372C");
    public static Color Green500 => FromHex("#2ABB7F");
    public static Color Green700 => FromHex("#1F845A");
    public static Color Orange400 => FromHex("#FCA700");
    public static Color Yellow300 => FromHex("#EED12B");
    public static Color Teal500 => FromHex("#42B2D7");
    public static Color Purple500 => FromHex("#BF63F3");
    public static Color Blue1000 => FromHex("#1C2B42");
    public static Color DoneBadgeBg => FromHex("#DCFFF1");

    public static Color BgPage => Neutral100;
    public static Color BgSurface => Neutral0;
    public static Color BgSidebar => Blue1000;
    public static Color Primary => Blue500;
    public static Color PrimaryHover => Blue600;
    public static Color PrimaryActive => Blue700;
    public static Color SidebarText => Neutral0;
    public static Color SidebarHover => Color.FromArgb(20, 255, 255, 255);
    public static Color Border => Neutral300;
    public static Color TextPrimary => Neutral1100;
    public static Color TextSecondary => Neutral700;
    public static Color Success => Green500;
    public static Color Warning => Orange400;
    public static Color Danger => Red500;
    public static Color DangerHover => Red700;
    public static Color StatusTodo => Neutral200;
    public static Color StatusInProgress => Blue100;
    public static Color StatusDone => DoneBadgeBg;
    public static Color StatusDoneText => Green700;
    public static Color StatusInProgressText => Blue600;
    public static Color StatusTodoText => Neutral700;
    public static Color BoardColumnBg => Neutral200;
    public static Color CardShadow => Color.FromArgb(20, 9, 30, 66);
    public static Color SelectionBg => FromHex("#EAF0FB");
    public static Color AlternateRowBg => FromHex("#FAFBFC");
    public static Color IssueKey => Blue600;

    public static Font FontH1 => new("Segoe UI", 20f, FontStyle.Bold);
    public static Font FontH2 => new("Segoe UI", 16f, FontStyle.Bold);
    public static Font FontBody => new("Segoe UI", 12f, FontStyle.Regular);
    public static Font FontSmall => new("Segoe UI", 11f, FontStyle.Regular);
    public static Font FontCaption => new("Segoe UI", 10f, FontStyle.Regular);
    public static Font FontColumnHeader => new("Segoe UI", 10f, FontStyle.Bold);

    public const int SidebarWidth = 240;
    public const int NavbarHeight = 64;
    public const int CardMinHeight = 80;
    public const int CardWidth = 272;
    public const int BorderRadius = 4;
    public const int Xs = 4;
    public const int Sm = 8;
    public const int Md = 12;
    public const int Lg = 16;
    public const int Xl = 24;
    public const int Padding = Sm;
    public const int PaddingLg = Lg;

    public static Button CreateFlatButton(string text, bool isPrimary) =>
        isPrimary ? JiraControlFactory.CreatePrimaryButton(text) : JiraControlFactory.CreateSecondaryButton(text);

    public static void ApplyFormTheme(Form form, bool surface = false)
    {
        form.BackColor = surface ? BgSurface : BgPage;
        form.Font = FontBody;
    }

    public static void ApplySurface(Control control, int padding = 0)
    {
        control.BackColor = BgSurface;
        control.ForeColor = TextPrimary;
        if (padding > 0)
        {
            control.Padding = new Padding(padding);
        }
    }

    public static void StyleListView(ListView listView)
    {
        listView.BorderStyle = BorderStyle.None;
        listView.BackColor = BgSurface;
        listView.ForeColor = TextPrimary;
        listView.Font = FontBody;
        listView.FullRowSelect = true;
    }

    public static void StyleDataGridView(DataGridView grid)
    {
        grid.BackgroundColor = BgSurface;
        grid.GridColor = Border;
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle.BackColor = BgPage;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
        grid.ColumnHeadersDefaultCellStyle.Font = FontSmall;
        grid.RowsDefaultCellStyle.BackColor = BgSurface;
        grid.RowsDefaultCellStyle.SelectionBackColor = SelectionBg;
        grid.RowsDefaultCellStyle.SelectionForeColor = TextPrimary;
        grid.AlternatingRowsDefaultCellStyle.BackColor = AlternateRowBg;
    }

    private static Color FromHex(string hex) => ColorTranslator.FromHtml(hex);
}

