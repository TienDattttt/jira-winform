using System.Drawing;
using System.Drawing.Drawing2D;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Helpers;

namespace JiraClone.WinForms.Theme;

public sealed class JiraBadge : UserControl
{
    private readonly Color _backColor;
    private readonly Color _textColor;

    private JiraBadge(string text, Color backColor, Color textColor)
    {
        BadgeText = text;
        _backColor = backColor;
        _textColor = textColor;

        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Font = JiraTheme.FontCaption;
        Height = 20;
        Padding = new Padding(8, 2, 8, 2);
        Margin = new Padding(4, 0, 4, 0);

        var textSize = TextRenderer.MeasureText(BadgeText, Font);
        Width = Math.Max(44, textSize.Width + Padding.Horizontal);
    }

    public string BadgeText { get; }

    public static JiraBadge ForStatus(IssueStatus status)
    {
        var text = IssueDisplayText.TranslateStatus(status);
        return status switch
        {
            IssueStatus.Backlog => new JiraBadge(text, JiraTheme.StatusTodo, JiraTheme.StatusTodoText),
            IssueStatus.Selected => new JiraBadge(text, JiraTheme.StatusTodo, JiraTheme.StatusTodoText),
            IssueStatus.InProgress => new JiraBadge(text, JiraTheme.StatusInProgress, JiraTheme.StatusInProgressText),
            IssueStatus.Done => new JiraBadge(text, JiraTheme.StatusDone, JiraTheme.StatusDoneText),
            _ => new JiraBadge(text, JiraTheme.StatusTodo, JiraTheme.StatusTodoText),
        };
    }

    public static JiraBadge ForPriority(IssuePriority priority)
    {
        return priority switch
        {
            IssuePriority.Low => new JiraBadge("LOW", Color.FromArgb(228, 252, 239), JiraTheme.Green700),
            IssuePriority.Medium => new JiraBadge("MEDIUM", Color.FromArgb(255, 244, 214), JiraTheme.Orange400),
            IssuePriority.High => new JiraBadge("HIGH", Color.FromArgb(255, 235, 233), JiraTheme.Red500),
            IssuePriority.Highest => new JiraBadge("HIGHEST", Color.FromArgb(255, 225, 224), JiraTheme.Red700),
            _ => new JiraBadge(priority.ToString().ToUpperInvariant(), JiraTheme.Neutral200, JiraTheme.TextSecondary),
        };
    }

    public static JiraBadge ForType(IssueType type)
    {
        var text = IssueDisplayText.TranslateType(type);
        return type switch
        {
            IssueType.Task => new JiraBadge(text, Color.FromArgb(231, 240, 255), JiraTheme.Blue600),
            IssueType.Bug => new JiraBadge(text, Color.FromArgb(255, 235, 233), JiraTheme.Red500),
            IssueType.Story => new JiraBadge(text, Color.FromArgb(228, 252, 239), JiraTheme.Green700),
            _ => new JiraBadge(text, Color.FromArgb(245, 234, 255), JiraTheme.Purple500),
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = GraphicsHelper.CreateRoundedPath(bounds, Height / 2);
        using var brush = new SolidBrush(_backColor);

        e.Graphics.FillPath(brush, path);

        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        TextRenderer.DrawText(e.Graphics, BadgeText, Font, bounds, _textColor, flags);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Height = 20;
        Invalidate();
    }
}
