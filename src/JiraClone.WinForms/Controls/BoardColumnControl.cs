using System.Drawing.Drawing2D;
using System.Drawing.Text;
using JiraClone.Application.Models;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Helpers;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public class BoardColumnControl : UserControl
{
    private static readonly Color DropHighlightColor = ColorTranslator.FromHtml("#0052CC");

    private readonly HeaderPanel _headerPanel;
    private readonly Panel _scrollHost;
    private readonly FlowLayoutPanel _issuesPanel;
    private readonly Label _emptyState;
    private readonly LinkLabel _createIssueLink;
    private readonly DropPlaceholderPanel _dropPlaceholder = new();
    private BoardColumnDto _column;
    private bool _dropHighlight;

    public BoardColumnControl(BoardColumnDto column)
    {
        _column = column;
        Width = 296;
        MinimumSize = new Size(296, 220);
        BackColor = JiraTheme.BoardColumnBg;
        Margin = new Padding(0);
        Padding = new Padding(0);

        _headerPanel = new HeaderPanel();
        _scrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = JiraTheme.BoardColumnBg,
            Padding = new Padding(0, JiraTheme.Sm, 0, JiraTheme.Sm),
        };
        _issuesPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = JiraTheme.BoardColumnBg,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        _emptyState = JiraControlFactory.CreateLabel("No issues in this column.", true);
        _emptyState.Dock = DockStyle.Top;
        _emptyState.Height = 40;
        _emptyState.TextAlign = ContentAlignment.MiddleCenter;
        _emptyState.ForeColor = JiraTheme.TextSecondary;
        _createIssueLink = new LinkLabel
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            Text = "+ Create issue",
            LinkColor = JiraTheme.Blue600,
            ActiveLinkColor = JiraTheme.PrimaryHover,
            VisitedLinkColor = JiraTheme.Blue600,
            Font = JiraTheme.FontSmall,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = JiraTheme.BoardColumnBg,
            Padding = new Padding(14, 0, 0, 0),
            AutoEllipsis = true,
        };

        _createIssueLink.Click += (_, _) => CreateIssueRequested?.Invoke(this, _column.Status);
        _scrollHost.Controls.Add(_issuesPanel);
        _scrollHost.Controls.Add(_emptyState);

        ConfigureDropTarget(this);
        ConfigureDropTarget(_scrollHost);
        ConfigureDropTarget(_issuesPanel);
        ConfigureDropTarget(_emptyState);

        Controls.Add(_scrollHost);
        Controls.Add(_createIssueLink);
        Controls.Add(_headerPanel);
        Resize += (_, _) => UpdateScrollMetrics();

        Bind(column);
    }

    public IssueStatus Status => _column.Status;

    public event EventHandler<int>? IssueSelected;
    public event EventHandler<IssueStatus>? CreateIssueRequested;
    public event EventHandler<IssueMoveRequestedEventArgs>? IssueMoveRequested;

    public void Bind(BoardColumnDto column, int? animatedIssueId = null)
    {
        _column = column;
        _headerPanel.Title = column.Name;
        _headerPanel.Count = column.Issues.Count;

        RemoveDropPlaceholder();
        _issuesPanel.SuspendLayout();
        _issuesPanel.Controls.Clear();

        foreach (var issue in column.Issues)
        {
            var card = new IssueCardControl(issue);
            card.Width = Math.Max(248, Width - SystemInformation.VerticalScrollBarWidth - 12);
            card.IssueSelected += (_, issueId) => IssueSelected?.Invoke(this, issueId);
            card.IssueMoveRequested += (_, args) => IssueMoveRequested?.Invoke(this, args);
            card.Height = card.GetPreferredSize(new Size(card.Width, 0)).Height;
            _issuesPanel.Controls.Add(card);

            if (animatedIssueId.HasValue && issue.Id == animatedIssueId.Value)
            {
                card.StartEntranceAnimation();
            }
        }

        _issuesPanel.ResumeLayout();
        _emptyState.Visible = column.Issues.Count == 0;
        UpdateScrollMetrics();
        _headerPanel.Invalidate();
        Invalidate();
    }

    private void UpdateScrollMetrics()
    {
        _issuesPanel.Width = Math.Max(248, ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);

        foreach (var control in _issuesPanel.Controls.OfType<IssueCardControl>())
        {
            control.Width = _issuesPanel.Width;
            control.Height = control.GetPreferredSize(new Size(control.Width, 0)).Height;
        }

        if (_dropPlaceholder.Parent == _issuesPanel)
        {
            _dropPlaceholder.Width = _issuesPanel.Width;
        }

        var totalHeight = _issuesPanel.Controls.Cast<Control>().Sum(x => x.Height + x.Margin.Vertical);
        if (_emptyState.Visible)
        {
            totalHeight += _emptyState.Height;
        }

        _scrollHost.AutoScrollMinSize = new Size(_issuesPanel.Width, totalHeight + JiraTheme.Md);
    }

    private void ConfigureDropTarget(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += OnDragEnter;
        control.DragOver += OnDragOver;
        control.DragLeave += OnDragLeave;
        control.DragDrop += OnDragDrop;
    }

    private bool TryGetDragData(IDataObject? dataObject, out IssueCardDragData dragData)
    {
        if (dataObject?.GetDataPresent(typeof(IssueCardDragData)) == true && dataObject.GetData(typeof(IssueCardDragData)) is IssueCardDragData payload)
        {
            dragData = payload;
            return true;
        }

        dragData = null!;
        return false;
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        UpdateDropState(e);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        UpdateDropState(e);
    }

    private void UpdateDropState(DragEventArgs e)
    {
        if (TryGetDragData(e.Data, out var dragData) && dragData.Issue.Status != _column.Status)
        {
            e.Effect = DragDropEffects.Move;
            SetDropHighlight(true);
            EnsureDropPlaceholder();
            return;
        }

        e.Effect = DragDropEffects.None;
        ClearDropState();
    }

    private void OnDragLeave(object? sender, EventArgs e)
    {
        var clientPoint = PointToClient(Cursor.Position);
        if (ClientRectangle.Contains(clientPoint))
        {
            return;
        }

        ClearDropState();
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        var hasDrop = TryGetDragData(e.Data, out var dragData) && dragData.Issue.Status != _column.Status;
        ClearDropState();
        if (hasDrop)
        {
            IssueMoveRequested?.Invoke(this, new IssueMoveRequestedEventArgs(dragData.Issue.Id, dragData.Issue.Status, _column.Status));
        }
    }

    private void EnsureDropPlaceholder()
    {
        if (_dropPlaceholder.Parent == _issuesPanel)
        {
            return;
        }

        _dropPlaceholder.Width = _issuesPanel.Width;
        _issuesPanel.Controls.Add(_dropPlaceholder);
        _issuesPanel.Controls.SetChildIndex(_dropPlaceholder, 0);
        _emptyState.Visible = false;
        UpdateScrollMetrics();
    }

    private void RemoveDropPlaceholder()
    {
        if (_dropPlaceholder.Parent != _issuesPanel)
        {
            return;
        }

        _issuesPanel.Controls.Remove(_dropPlaceholder);
        _emptyState.Visible = _column.Issues.Count == 0;
        UpdateScrollMetrics();
    }

    private void ClearDropState()
    {
        SetDropHighlight(false);
        RemoveDropPlaceholder();
    }

    private void SetDropHighlight(bool highlighted)
    {
        if (_dropHighlight == highlighted)
        {
            return;
        }

        _dropHighlight = highlighted;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_dropHighlight)
        {
            using var pen = new Pen(DropHighlightColor, 3f);
            e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
        }
    }

    private sealed class HeaderPanel : Panel
    {
        public HeaderPanel()
        {
            Dock = DockStyle.Top;
            Height = 46;
            BackColor = JiraTheme.BoardColumnBg;
            DoubleBuffered = true;
        }

        public string Title { get; set; } = string.Empty;
        public int Count { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SetClip(new Rectangle(0, 0, Width, Height));
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var borderPen = new Pen(JiraTheme.Border);
            e.Graphics.DrawLine(borderPen, 0, Height - 1, Width, Height - 1);

            var titleBounds = new Rectangle(12, 0, Width - 92, Height);
            TextRenderer.DrawText(
                e.Graphics,
                Title.ToUpperInvariant(),
                JiraTheme.FontColumnHeader,
                titleBounds,
                JiraTheme.TextSecondary,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

            var countText = Count.ToString();
            var textSize = TextRenderer.MeasureText(countText, JiraTheme.FontCaption);
            var badgeBounds = new Rectangle(Width - textSize.Width - 30, 12, textSize.Width + 18, 20);

            using var badgePath = GraphicsHelper.CreateRoundedPath(badgeBounds, 10);
            using var badgeBrush = new SolidBrush(JiraTheme.Neutral0);
            e.Graphics.FillPath(badgeBrush, badgePath);

            TextRenderer.DrawText(
                e.Graphics,
                countText,
                JiraTheme.FontCaption,
                badgeBounds,
                JiraTheme.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private sealed class DropPlaceholderPanel : Panel
    {
        public DropPlaceholderPanel()
        {
            Height = JiraTheme.CardMinHeight + JiraTheme.Sm;
            Margin = new Padding(0, 0, 0, JiraTheme.Sm);
            BackColor = Color.Transparent;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Math.Max(0, Width - 4), Math.Max(0, Height - 4));
            using var path = GraphicsHelper.CreateRoundedPath(bounds, JiraTheme.BorderRadius);
            using var fillBrush = new SolidBrush(Color.FromArgb(28, DropHighlightColor));
            using var borderPen = new Pen(DropHighlightColor, 2f) { DashStyle = DashStyle.Dash };
            e.Graphics.FillPath(fillBrush, path);
            e.Graphics.DrawPath(borderPen, path);

            TextRenderer.DrawText(
                e.Graphics,
                "Drop issue here",
                JiraTheme.FontCaption,
                bounds,
                JiraTheme.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
