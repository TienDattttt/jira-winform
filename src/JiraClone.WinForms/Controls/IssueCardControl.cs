using System.Drawing.Drawing2D;
using System.Drawing.Text;
using JiraClone.Application.Models;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public class IssueCardControl : UserControl
{
    private bool _hovered;

    public IssueCardControl(IssueSummaryDto issue)
    {
        Issue = issue;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        Width = 296;
        MinimumSize = new Size(296, JiraTheme.CardMinHeight);
        Margin = new Padding(0, 0, 0, JiraTheme.Sm);
        Padding = new Padding(JiraTheme.Md);

        HookClicks(this);
        MouseEnter += (_, _) => SetHover(true);
        MouseLeave += (_, _) => SetHover(false);
    }

    public IssueSummaryDto Issue { get; }

    public event EventHandler<int>? IssueSelected;
    public event EventHandler<IssueMoveRequestedEventArgs>? IssueMoveRequested;

    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = Math.Max(296, proposedSize.Width > 0 ? proposedSize.Width : Width);
        var textAreaWidth = width - Padding.Horizontal - 12;
        var measured = TextRenderer.MeasureText(
            Issue.Title,
            JiraTheme.FontBody,
            new Size(Math.Max(100, textAreaWidth), 0),
            TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.NoPadding);

        var height = Math.Max(JiraTheme.CardMinHeight, Padding.Top + measured.Height + 42 + Padding.Bottom);
        return new Size(width, height);
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        Height = GetPreferredSize(new Size(Width, 0)).Height;
        ConfigureContextMenu();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SetClip(new Rectangle(0, 0, Width, Height));
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var cardBounds = new Rectangle(0, 0, Width - 4, Height - 4);
        var shadowBounds = new Rectangle(1, 2, Width - 4, Height - 4);

        if (_hovered)
        {
            using var shadowPath = CreateRoundedPath(shadowBounds, JiraTheme.BorderRadius);
            using var shadowBrush = new SolidBrush(JiraTheme.CardShadow);
            e.Graphics.FillPath(shadowBrush, shadowPath);
        }

        using var cardPath = CreateRoundedPath(cardBounds, JiraTheme.BorderRadius);
        using var fillBrush = new SolidBrush(JiraTheme.BgSurface);
        using var borderPen = new Pen(_hovered ? JiraTheme.Blue600 : JiraTheme.Border);
        e.Graphics.FillPath(fillBrush, cardPath);
        e.Graphics.DrawPath(borderPen, cardPath);

        var titleBounds = new Rectangle(Padding.Left, Padding.Top + 2, Width - Padding.Horizontal - 12, Height - Padding.Vertical - 42);
        var titleHeight = TextRenderer.MeasureText(
            Issue.Title,
            JiraTheme.FontBody,
            new Size(titleBounds.Width, 0),
            TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.NoPadding).Height;
        TextRenderer.DrawText(
            e.Graphics,
            Issue.Title,
            JiraTheme.FontBody,
            titleBounds,
            JiraTheme.TextPrimary,
            TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);

        var bottomY = Math.Max(Padding.Top + titleHeight + 8, Height - Padding.Bottom - 24);
        using var typeIcon = JiraIcons.GetIssueTypeIcon(Issue.Type, 16);
        e.Graphics.DrawImage(typeIcon, Padding.Left, bottomY + 2, 16, 16);

        var issueKeyBounds = new Rectangle(Padding.Left + 22, bottomY, 104, 20);
        TextRenderer.DrawText(
            e.Graphics,
            Issue.IssueKey,
            JiraTheme.FontCaption,
            issueKeyBounds,
            JiraTheme.IssueKey,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        using var priorityIcon = JiraIcons.GetPriorityIcon(Issue.Priority, 16);
        e.Graphics.DrawImage(priorityIcon, issueKeyBounds.Right + 4, bottomY + 2, 16, 16);

        var avatarBounds = new Rectangle(Width - Padding.Right - 24 - 4, bottomY - 2, 24, 24);
        using var avatarBrush = new SolidBrush(JiraTheme.Blue600);
        e.Graphics.FillEllipse(avatarBrush, avatarBounds);
        TextRenderer.DrawText(
            e.Graphics,
            BuildInitials(),
            new Font("Segoe UI", 8f, FontStyle.Bold),
            avatarBounds,
            Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private void SetHover(bool hovered)
    {
        _hovered = hovered;
        Invalidate();
    }

    private void ConfigureContextMenu()
    {
        var menu = new ContextMenuStrip();
        foreach (var status in Enum.GetValues<IssueStatus>().Where(x => x != Issue.Status))
        {
            menu.Items.Add($"Move to {FormatStatus(status)}", null, (_, _) =>
                IssueMoveRequested?.Invoke(this, new IssueMoveRequestedEventArgs(Issue.Id, status)));
        }

        ContextMenuStrip = menu;
    }

    private void HookClicks(Control control)
    {
        control.Click += (_, _) => IssueSelected?.Invoke(this, Issue.Id);

        foreach (Control child in control.Controls)
        {
            HookClicks(child);
        }

        control.ControlAdded += (_, args) =>
        {
            if (args.Control is not null)
            {
                HookClicks(args.Control);
            }
        };
    }

    private string BuildInitials()
    {
        var source = Issue.AssigneeNames.FirstOrDefault() ?? Issue.ReporterName ?? Issue.IssueKey;
        var initials = source
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(x => char.ToUpperInvariant(x[0]))
            .ToArray();

        return initials.Length == 0 ? "?" : new string(initials);
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var diameter = Math.Max(2, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static string FormatStatus(IssueStatus status) => status switch
    {
        IssueStatus.InProgress => "In Progress",
        _ => status.ToString()
    };
}

public sealed record IssueMoveRequestedEventArgs(int IssueId, IssueStatus TargetStatus);

