using System.Drawing.Drawing2D;
using System.Drawing.Text;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public class LabelChipControl : Control
{
    private Color _chipColor = JiraTheme.Blue500;

    public LabelChipControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Font = JiraTheme.FontCaption;
        Padding = new Padding(12, 6, 12, 6);
        Height = 28;
        UpdateSize();
    }

    public string ChipText
    {
        get => base.Text ?? string.Empty;
        set
        {
            base.Text = value;
            UpdateSize();
            Invalidate();
        }
    }

    public Color ChipColor
    {
        get => _chipColor;
        set
        {
            _chipColor = value;
            Invalidate();
        }
    }

    public string ColorHex
    {
        get => ColorTranslator.ToHtml(_chipColor);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                ChipColor = JiraTheme.Blue500;
                return;
            }

            ChipColor = ColorTranslator.FromHtml(value);
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var textSize = TextRenderer.MeasureText(ChipText + "  ", Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
        return new Size(Math.Max(64, textSize.Width + Padding.Horizontal), 28);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = CreateRoundedPath(bounds, 14);
        using var fill = new SolidBrush(_chipColor);
        using var border = new Pen(ControlPaint.Dark(_chipColor, 0.08f));
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);

        var foreColor = GetContrastColor(_chipColor);
        var textBounds = Rectangle.Inflate(bounds, -10, -4);
        TextRenderer.DrawText(e.Graphics, ChipText, Font, textBounds, foreColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void UpdateSize()
    {
        Size = GetPreferredSize(Size.Empty);
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color GetContrastColor(Color color)
    {
        var luminance = ((0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B)) / 255d;
        return luminance > 0.6 ? JiraTheme.TextPrimary : Color.White;
    }
}



