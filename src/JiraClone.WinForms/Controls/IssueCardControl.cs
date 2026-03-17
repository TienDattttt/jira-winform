using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using JiraClone.Application.Models;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Helpers;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public class IssueCardControl : UserControl
{
    private static readonly Font AvatarFont = new("Segoe UI", 8f, FontStyle.Bold);
    private static readonly Color DragAccent = ColorTranslator.FromHtml("#0052CC");
    private const int DragThreshold = 6;

    private readonly System.Windows.Forms.Timer _entranceTimer = new() { Interval = 15 };
    private bool _hovered;
    private bool _dragging;
    private Point _mouseDownPos;
    private Bitmap? _ghostBitmap;
    private Cursor? _ghostCursor;
    private float _entranceProgress = 1f;

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
        _entranceTimer.Tick += OnEntranceTimerTick;
        MouseEnter += (_, _) => SetHover(true);
        MouseLeave += (_, _) => SetHover(false);
        MouseDown += OnCardMouseDown;
        MouseMove += OnCardMouseMove;
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

        var graphicsState = e.Graphics.Save();
        if (_entranceProgress < 1f)
        {
            var entranceOffset = (1f - _entranceProgress) * 18f;
            e.Graphics.TranslateTransform(entranceOffset, 0f);
        }

        var cardBounds = new Rectangle(0, 0, Width - 4, Height - 4);
        var shadowBounds = new Rectangle(1, 2, Width - 4, Height - 4);
        var isPlaceholder = _dragging;

        if (_hovered && !isPlaceholder)
        {
            using var shadowPath = GraphicsHelper.CreateRoundedPath(shadowBounds, JiraTheme.BorderRadius);
            using var shadowBrush = new SolidBrush(JiraTheme.CardShadow);
            e.Graphics.FillPath(shadowBrush, shadowPath);
        }

        using var cardPath = GraphicsHelper.CreateRoundedPath(cardBounds, JiraTheme.BorderRadius);
        using var fillBrush = new SolidBrush(isPlaceholder
            ? Color.FromArgb(220, JiraTheme.Neutral0)
            : JiraTheme.BgSurface);
        using var borderPen = new Pen(
            isPlaceholder ? DragAccent : (_hovered ? JiraTheme.Blue600 : JiraTheme.Border),
            isPlaceholder ? 2f : 1f);
        if (isPlaceholder)
        {
            borderPen.DashStyle = DashStyle.Dash;
        }

        e.Graphics.FillPath(fillBrush, cardPath);
        e.Graphics.DrawPath(borderPen, cardPath);

        var contentTop = Padding.Top + 2;

        if (!string.IsNullOrEmpty(Issue.ParentIssueKey))
        {
            var parentBounds = new Rectangle(Padding.Left, contentTop, Width - Padding.Horizontal - 12, 16);
            TextRenderer.DrawText(
                e.Graphics,
                Issue.ParentIssueKey,
                JiraTheme.FontCaption,
                parentBounds,
                isPlaceholder ? JiraTheme.TextSecondary : JiraTheme.IssueKey,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
            contentTop += 18;
        }

        var titleBounds = new Rectangle(Padding.Left, contentTop, Width - Padding.Horizontal - 12, Height - Padding.Vertical - 42 - (contentTop - Padding.Top - 2));
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
            isPlaceholder ? JiraTheme.TextSecondary : JiraTheme.TextPrimary,
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
            isPlaceholder ? JiraTheme.TextSecondary : JiraTheme.IssueKey,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        using var priorityIcon = JiraIcons.GetPriorityIcon(Issue.Priority, 16);
        e.Graphics.DrawImage(priorityIcon, issueKeyBounds.Right + 4, bottomY + 2, 16, 16);

        var avatarBounds = new Rectangle(Width - Padding.Right - 24 - 4, bottomY - 2, 24, 24);
        using var avatarBrush = new SolidBrush(isPlaceholder ? JiraTheme.Neutral500 : JiraTheme.Blue600);
        e.Graphics.FillEllipse(avatarBrush, avatarBounds);
        TextRenderer.DrawText(
            e.Graphics,
            BuildInitials(),
            AvatarFont,
            avatarBounds,
            Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        if (_entranceProgress < 1f)
        {
            using var flashBrush = new SolidBrush(Color.FromArgb((int)Math.Round((1f - _entranceProgress) * 48f), JiraTheme.Blue100));
            using var flashPen = new Pen(Color.FromArgb((int)Math.Round((1f - _entranceProgress) * 120f), JiraTheme.Blue600));
            e.Graphics.FillPath(flashBrush, cardPath);
            e.Graphics.DrawPath(flashPen, cardPath);
        }

        e.Graphics.Restore(graphicsState);
    }

    public void StartEntranceAnimation()
    {
        _entranceProgress = 0f;
        _entranceTimer.Stop();
        _entranceTimer.Start();
        Invalidate();
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
                IssueMoveRequested?.Invoke(this, new IssueMoveRequestedEventArgs(Issue.Id, Issue.Status, status)));
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

    private static string FormatStatus(IssueStatus status) => status switch
    {
        IssueStatus.InProgress => "In Progress",
        _ => status.ToString()
    };

    private void OnCardMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _mouseDownPos = e.Location;
        }
    }

    private void OnCardMouseMove(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _dragging)
        {
            return;
        }

        if (Math.Abs(e.X - _mouseDownPos.X) < DragThreshold && Math.Abs(e.Y - _mouseDownPos.Y) < DragThreshold)
        {
            return;
        }

        _ghostBitmap?.Dispose();
        _ghostBitmap = CreateGhostBitmap();
        _ghostCursor?.Dispose();
        _ghostCursor = CreateCursorFromBitmap(_ghostBitmap);

        _dragging = true;
        Invalidate();

        var data = new DataObject();
        data.SetData(typeof(IssueCardDragData), new IssueCardDragData(Issue));
        data.SetData(typeof(IssueSummaryDto), Issue);

        GiveFeedback += OnGiveFeedback;
        try
        {
            DoDragDrop(data, DragDropEffects.Move);
        }
        finally
        {
            GiveFeedback -= OnGiveFeedback;
            _dragging = false;
            _ghostCursor?.Dispose();
            _ghostCursor = null;
            _ghostBitmap?.Dispose();
            _ghostBitmap = null;
            SetHover(false);
            Invalidate();
        }
    }

    private void OnGiveFeedback(object? sender, GiveFeedbackEventArgs e)
    {
        e.UseDefaultCursors = false;
        if (_ghostCursor is not null)
        {
            Cursor.Current = _ghostCursor;
        }
    }

    private void OnEntranceTimerTick(object? sender, EventArgs e)
    {
        _entranceProgress = Math.Min(1f, _entranceProgress + 0.14f);
        if (_entranceProgress >= 1f)
        {
            _entranceTimer.Stop();
        }

        Invalidate();
    }

    private Bitmap CreateGhostBitmap()
    {
        var bmp = new Bitmap(Width, Height);
        DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));

        var ghost = new Bitmap(bmp.Width, bmp.Height);
        using var g = Graphics.FromImage(ghost);
        using var attrs = new ImageAttributes();
        var matrix = new ColorMatrix { Matrix33 = 0.7f };
        attrs.SetColorMatrix(matrix);
        g.DrawImage(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attrs);
        bmp.Dispose();
        return ghost;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr CreateIconIndirect(ref IconInfo icon);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetIconInfo(IntPtr hIcon, ref IconInfo pIconInfo);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    private static Cursor CreateCursorFromBitmap(Bitmap bitmap)
    {
        var cursorSize = new Size(Math.Min(bitmap.Width, 150), Math.Min(bitmap.Height, 80));
        using var scaled = new Bitmap(bitmap, cursorSize);
        var hIcon = scaled.GetHicon();
        var info = new IconInfo();
        GetIconInfo(hIcon, ref info);
        info.fIcon = false;
        info.xHotspot = cursorSize.Width / 2;
        info.yHotspot = cursorSize.Height / 2;
        var cursorPtr = CreateIconIndirect(ref info);
        DestroyIcon(hIcon);
        if (info.hbmColor != IntPtr.Zero)
        {
            DeleteObject(info.hbmColor);
        }

        if (info.hbmMask != IntPtr.Zero)
        {
            DeleteObject(info.hbmMask);
        }

        return new Cursor(cursorPtr);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _entranceTimer.Dispose();
            _ghostCursor?.Dispose();
            _ghostBitmap?.Dispose();
        }

        base.Dispose(disposing);
    }
}

public sealed record IssueCardDragData(IssueSummaryDto Issue);
public sealed record IssueMoveRequestedEventArgs(int IssueId, IssueStatus SourceStatus, IssueStatus TargetStatus);
