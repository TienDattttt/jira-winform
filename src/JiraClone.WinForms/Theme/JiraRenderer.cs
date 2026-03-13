using System.Drawing;
using System.Drawing.Drawing2D;

namespace JiraClone.WinForms.Theme;

public sealed class JiraRenderer : ToolStripProfessionalRenderer
{
    public JiraRenderer()
        : base(new JiraColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        if (e.ToolStrip is MenuStrip or ToolStrip)
        {
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawLine(pen, 0, e.ToolStrip.Height - 1, e.ToolStrip.Width, e.ToolStrip.Height - 1);
            return;
        }

        base.OnRenderToolStripBorder(e);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var bounds = new Rectangle(Point.Empty, e.Item.Size);
        var backColor = e.Item.Selected ? JiraTheme.Primary : JiraTheme.BgSurface;

        using var brush = new SolidBrush(backColor);
        e.Graphics.FillRectangle(brush, bounds);

        e.Item.ForeColor = e.Item.Selected ? Color.White : JiraTheme.TextPrimary;
    }

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        var bounds = new Rectangle(Point.Empty, e.Item.Size);
        var backColor = e.Item.Selected || e.Item.Pressed ? JiraTheme.Primary : JiraTheme.BgSurface;

        using var brush = new SolidBrush(backColor);
        e.Graphics.FillRectangle(brush, bounds);

        e.Item.ForeColor = e.Item.Selected || e.Item.Pressed ? Color.White : JiraTheme.TextPrimary;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Selected || e.Item.Pressed
            ? Color.White
            : JiraTheme.TextPrimary;

        base.OnRenderItemText(e);
    }

    private sealed class JiraColorTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin => JiraTheme.BgSurface;
        public override Color MenuStripGradientEnd => JiraTheme.BgSurface;
        public override Color ToolStripDropDownBackground => JiraTheme.BgSurface;
        public override Color ImageMarginGradientBegin => JiraTheme.BgSurface;
        public override Color ImageMarginGradientMiddle => JiraTheme.BgSurface;
        public override Color ImageMarginGradientEnd => JiraTheme.BgSurface;
        public override Color ToolStripBorder => JiraTheme.Border;
        public override Color MenuBorder => JiraTheme.Border;
        public override Color SeparatorDark => JiraTheme.Border;
        public override Color SeparatorLight => JiraTheme.Border;
        public override Color ButtonSelectedHighlight => JiraTheme.Primary;
        public override Color ButtonSelectedHighlightBorder => JiraTheme.Primary;
        public override Color ButtonPressedHighlight => JiraTheme.Primary;
        public override Color ButtonPressedHighlightBorder => JiraTheme.Primary;
        public override Color ButtonCheckedHighlight => JiraTheme.Primary;
        public override Color ButtonCheckedHighlightBorder => JiraTheme.Primary;
        public override Color MenuItemSelected => JiraTheme.Primary;
        public override Color MenuItemBorder => JiraTheme.Primary;
        public override Color MenuItemSelectedGradientBegin => JiraTheme.Primary;
        public override Color MenuItemSelectedGradientEnd => JiraTheme.Primary;
        public override Color MenuItemPressedGradientBegin => JiraTheme.BgSurface;
        public override Color MenuItemPressedGradientMiddle => JiraTheme.BgSurface;
        public override Color MenuItemPressedGradientEnd => JiraTheme.BgSurface;
        public override Color ToolStripGradientBegin => JiraTheme.BgSurface;
        public override Color ToolStripGradientMiddle => JiraTheme.BgSurface;
        public override Color ToolStripGradientEnd => JiraTheme.BgSurface;
    }
}
