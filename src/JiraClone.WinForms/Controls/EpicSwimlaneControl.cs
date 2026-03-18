using System.Drawing.Drawing2D;
using JiraClone.Application.Models;
using JiraClone.WinForms.Helpers;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public sealed class EpicSwimlaneControl : UserControl
{
    private readonly SwimlaneHeaderPanel _headerPanel = new();
    private readonly FlowLayoutPanel _columnsPanel = new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = false,
        BackColor = Color.Transparent,
        Margin = new Padding(0),
        Padding = new Padding(0, 8, 0, 0),
    };

    private EpicSwimlaneViewModel _lane = EpicSwimlaneViewModel.Empty;
    private bool _showStoryPointProgress;

    public EpicSwimlaneControl(EpicSwimlaneViewModel lane, bool showStoryPointProgress = false)
    {
        _lane = lane;
        _showStoryPointProgress = showStoryPointProgress;
        BackColor = Color.Transparent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Margin = new Padding(0, 0, 0, 16);
        Padding = new Padding(0);
        DoubleBuffered = true;

        _headerPanel.Dock = DockStyle.Top;
        _headerPanel.ToggleRequested += (_, _) => CollapseChanged?.Invoke(this, new EpicSwimlaneCollapseChangedEventArgs(_lane.LaneKey, !_lane.IsCollapsed));
        _headerPanel.EpicRequested += (_, _) =>
        {
            if (_lane.EpicId.HasValue)
            {
                EpicSelected?.Invoke(this, _lane.EpicId.Value);
            }
        };

        Controls.Add(_columnsPanel);
        Controls.Add(_headerPanel);
        Bind(lane, showStoryPointProgress: showStoryPointProgress);
    }

    public event EventHandler<int>? IssueSelected;
    public event EventHandler<int>? EpicSelected;
    public event EventHandler<IssueMoveRequestedEventArgs>? IssueMoveRequested;
    public event EventHandler<EpicSwimlaneCollapseChangedEventArgs>? CollapseChanged;
    public event EventHandler<BoardColumnWipLimitEventArgs>? WipLimitWarningRequested;

    public void Bind(EpicSwimlaneViewModel lane, int? animatedIssueId = null, bool? showStoryPointProgress = null)
    {
        _lane = lane;
        _showStoryPointProgress = showStoryPointProgress ?? _showStoryPointProgress;
        _headerPanel.Bind(lane, _showStoryPointProgress);

        _columnsPanel.SuspendLayout();
        try
        {
            foreach (Control control in _columnsPanel.Controls)
            {
                control.Dispose();
            }

            _columnsPanel.Controls.Clear();
            foreach (var column in lane.Columns)
            {
                var cell = new SwimlaneStatusCellControl(lane.EpicId, column)
                {
                    Margin = new Padding(0, 0, 16, 0)
                };
                cell.IssueSelected += (_, issueId) => IssueSelected?.Invoke(this, issueId);
                cell.IssueMoveRequested += (_, args) => IssueMoveRequested?.Invoke(this, args);
                cell.WipLimitWarningRequested += (_, args) => WipLimitWarningRequested?.Invoke(this, args);
                cell.Bind(column, animatedIssueId);
                _columnsPanel.Controls.Add(cell);
            }
        }
        finally
        {
            _columnsPanel.ResumeLayout();
        }

        _columnsPanel.Visible = !lane.IsCollapsed;
    }

    private sealed class SwimlaneHeaderPanel : Panel
    {
        private readonly Button _toggleButton = JiraControlFactory.CreateSecondaryButton("-");
        private readonly LinkLabel _titleLink = new()
        {
            Dock = DockStyle.Fill,
            LinkBehavior = LinkBehavior.HoverUnderline,
            Font = JiraTheme.FontBody,
            AutoEllipsis = true,
            ActiveLinkColor = JiraTheme.PrimaryActive,
            LinkColor = JiraTheme.TextPrimary,
            VisitedLinkColor = JiraTheme.TextPrimary,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        private readonly Label _summaryLabel = JiraControlFactory.CreateLabel(string.Empty, true);
        private readonly SwimlaneProgressBar _progressBar = new() { Dock = DockStyle.Top, Height = 8, Margin = new Padding(0, 0, 0, 6) };
        private readonly Panel _accent = new() { Dock = DockStyle.Left, Width = 8 };

        public SwimlaneHeaderPanel()
        {
            Height = 86;
            BackColor = JiraTheme.BgSurface;
            Padding = new Padding(16, 12, 16, 12);
            DoubleBuffered = true;

            _toggleButton.AutoSize = false;
            _toggleButton.Size = new Size(36, 32);
            _toggleButton.FlatAppearance.BorderColor = JiraTheme.Border;
            _toggleButton.Margin = new Padding(0);
            _toggleButton.Click += (_, _) => ToggleRequested?.Invoke(this, EventArgs.Empty);

            _titleLink.LinkClicked += (_, _) => EpicRequested?.Invoke(this, EventArgs.Empty);
            _titleLink.Click += (_, _) =>
            {
                if (_titleLink.Links.Count > 0)
                {
                    EpicRequested?.Invoke(this, EventArgs.Empty);
                }
            };

            _summaryLabel.Dock = DockStyle.Top;
            _summaryLabel.Font = JiraTheme.FontCaption;
            _summaryLabel.ForeColor = JiraTheme.TextSecondary;
            _summaryLabel.Height = 20;

            var textPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _titleLink.Dock = DockStyle.Top;
            _titleLink.Height = 28;
            textPanel.Controls.Add(_summaryLabel);
            textPanel.Controls.Add(_progressBar);
            textPanel.Controls.Add(_titleLink);

            var actionPanel = new Panel { Dock = DockStyle.Right, Width = 44, BackColor = Color.Transparent };
            actionPanel.Controls.Add(_toggleButton);
            _toggleButton.Location = new Point(4, 8);

            Controls.Add(textPanel);
            Controls.Add(actionPanel);
            Controls.Add(_accent);
        }

        public event EventHandler? ToggleRequested;
        public event EventHandler? EpicRequested;

        public void Bind(EpicSwimlaneViewModel lane, bool showStoryPointProgress)
        {
            _accent.BackColor = lane.HeaderColor;
            _toggleButton.Text = lane.IsCollapsed ? "+" : "-";
            _toggleButton.Enabled = lane.Columns.Sum(x => x.Issues.Count) > 0;
            _titleLink.Text = lane.DisplayTitle;
            _titleLink.Links.Clear();
            if (lane.EpicId.HasValue)
            {
                _titleLink.Links.Add(0, lane.DisplayTitle.Length, lane.EpicId.Value);
                _titleLink.LinkColor = JiraTheme.TextPrimary;
            }
            else
            {
                _titleLink.LinkColor = JiraTheme.TextPrimary;
            }

            _summaryLabel.Text = showStoryPointProgress
                ? $"{lane.DoneStoryPoints}/{lane.TotalStoryPoints} story points completed"
                : $"{lane.DoneIssues}/{lane.TotalIssues} issues done";
            _progressBar.Bind(showStoryPointProgress ? lane.DoneStoryPoints : lane.DoneIssues, showStoryPointProgress ? lane.TotalStoryPoints : lane.TotalIssues, lane.HeaderColor);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using var pen = new Pen(JiraTheme.Border);
            using var fill = new SolidBrush(JiraTheme.BgSurface);
            using var path = GraphicsHelper.CreateRoundedPath(bounds, JiraTheme.BorderRadius);
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(pen, path);
        }
    }

    private sealed class SwimlaneProgressBar : Control
    {
        private int _completed;
        private int _total;
        private Color _accent = JiraTheme.Blue600;

        public SwimlaneProgressBar()
        {
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        public void Bind(int completed, int total, Color accent)
        {
            _completed = completed;
            _total = total;
            _accent = accent;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 1, Width - 1, Height - 3);
            using var backgroundPath = GraphicsHelper.CreateRoundedPath(bounds, 3);
            using var backgroundBrush = new SolidBrush(JiraTheme.Neutral200);
            e.Graphics.FillPath(backgroundBrush, backgroundPath);

            if (_total <= 0 || _completed <= 0)
            {
                return;
            }

            var progressWidth = Math.Max(6, (int)Math.Round(bounds.Width * Math.Min(1d, _completed / (double)_total)));
            var progressBounds = new Rectangle(bounds.X, bounds.Y, Math.Min(bounds.Width, progressWidth), bounds.Height);
            using var progressPath = GraphicsHelper.CreateRoundedPath(progressBounds, 3);
            using var progressBrush = new SolidBrush(_accent);
            e.Graphics.FillPath(progressBrush, progressPath);
        }
    }

    private sealed class SwimlaneStatusCellControl : UserControl
    {
        private static readonly Color DropHighlightColor = ColorTranslator.FromHtml("#0052CC");
        private static readonly Color WipLimitHighlightColor = ColorTranslator.FromHtml("#DE350B");

        private readonly int? _laneEpicId;
        private readonly Panel _headerPanel = new() { Dock = DockStyle.Top, Height = 34, BackColor = JiraTheme.BoardColumnBg, Padding = new Padding(12, 8, 12, 6) };
        private readonly Label _titleLabel = JiraControlFactory.CreateLabel(string.Empty, true);
        private readonly Label _countLabel = JiraControlFactory.CreateLabel(string.Empty, true);
        private readonly FlowLayoutPanel _issuesPanel = new()
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
        private readonly Panel _bodyPanel = new() { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = JiraTheme.BoardColumnBg, Padding = new Padding(8, 8, 8, 8) };
        private readonly Label _emptyState = JiraControlFactory.CreateLabel("No issues", true);
        private readonly DropPlaceholderPanel _dropPlaceholder = new();
        private EpicSwimlaneColumnViewModel _column;
        private bool _dropHighlight;
        private bool _wipWarningRaised;
        private Color _dropHighlightColor = DropHighlightColor;

        public SwimlaneStatusCellControl(int? laneEpicId, EpicSwimlaneColumnViewModel column)
        {
            _laneEpicId = laneEpicId;
            _column = column;
            Width = 296;
            MinimumSize = new Size(296, 120);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Color.Transparent;
            Margin = new Padding(0);
            Padding = new Padding(0);

            _titleLabel.Dock = DockStyle.Left;
            _titleLabel.Font = JiraTheme.FontCaption;
            _titleLabel.ForeColor = JiraTheme.TextSecondary;
            _titleLabel.AutoSize = false;
            _titleLabel.Width = 180;
            _countLabel.Dock = DockStyle.Right;
            _countLabel.Font = JiraTheme.FontCaption;
            _countLabel.ForeColor = JiraTheme.TextSecondary;
            _countLabel.TextAlign = ContentAlignment.MiddleRight;
            _countLabel.Width = 72;
            _headerPanel.Controls.Add(_countLabel);
            _headerPanel.Controls.Add(_titleLabel);

            _emptyState.Dock = DockStyle.Top;
            _emptyState.Height = 28;
            _emptyState.TextAlign = ContentAlignment.MiddleCenter;
            _emptyState.ForeColor = JiraTheme.TextSecondary;

            _bodyPanel.Controls.Add(_issuesPanel);
            _bodyPanel.Controls.Add(_emptyState);

            ConfigureDropTarget(this);
            ConfigureDropTarget(_bodyPanel);
            ConfigureDropTarget(_issuesPanel);
            ConfigureDropTarget(_emptyState);

            Controls.Add(_bodyPanel);
            Controls.Add(_headerPanel);
            Resize += OnCellResize;
            Bind(column);
        }

        public event EventHandler<int>? IssueSelected;
        public event EventHandler<IssueMoveRequestedEventArgs>? IssueMoveRequested;
        public event EventHandler<BoardColumnWipLimitEventArgs>? WipLimitWarningRequested;

        public void Bind(EpicSwimlaneColumnViewModel column, int? animatedIssueId = null)
        {
            _column = column;
            _titleLabel.Text = column.Name.ToUpperInvariant();
            _countLabel.Text = column.Issues.Count.ToString();
            RemoveDropPlaceholder();
            DetachIssueCards();
            _issuesPanel.SuspendLayout();
            try
            {
                foreach (Control control in _issuesPanel.Controls)
                {
                    control.Dispose();
                }

                _issuesPanel.Controls.Clear();
                foreach (var issue in column.Issues)
                {
                    var card = new IssueCardControl(issue)
                    {
                        Width = Math.Max(248, Width - 24)
                    };
                    card.IssueSelected += OnCardIssueSelected;
                    card.Height = card.GetPreferredSize(new Size(card.Width, 0)).Height;
                    _issuesPanel.Controls.Add(card);
                    if (animatedIssueId.HasValue && issue.Id == animatedIssueId.Value)
                    {
                        card.StartEntranceAnimation();
                    }
                }
            }
            finally
            {
                _issuesPanel.ResumeLayout();
            }

            _emptyState.Visible = column.Issues.Count == 0;
            UpdateCardSizes();
            Invalidate();
        }

        private void UpdateCardSizes()
        {
            foreach (var card in _issuesPanel.Controls.OfType<IssueCardControl>())
            {
                card.Width = Math.Max(248, Width - 24);
                card.Height = card.GetPreferredSize(new Size(card.Width, 0)).Height;
            }

            if (_dropPlaceholder.Parent == _issuesPanel)
            {
                _dropPlaceholder.Width = Math.Max(248, Width - 24);
            }
        }

        private void OnCellResize(object? sender, EventArgs e)
        {
            UpdateCardSizes();
        }

        private void ConfigureDropTarget(Control control)
        {
            control.AllowDrop = true;
            control.DragEnter += OnDragEnter;
            control.DragOver += OnDragOver;
            control.DragLeave += OnDragLeave;
            control.DragDrop += OnDragDrop;
        }

        private void UnconfigureDropTarget(Control control)
        {
            control.DragEnter -= OnDragEnter;
            control.DragOver -= OnDragOver;
            control.DragLeave -= OnDragLeave;
            control.DragDrop -= OnDragDrop;
        }

        private static bool TryGetDragData(IDataObject? dataObject, out IssueCardDragData dragData)
        {
            if (dataObject?.GetDataPresent(typeof(IssueCardDragData)) == true && dataObject.GetData(typeof(IssueCardDragData)) is IssueCardDragData payload)
            {
                dragData = payload;
                return true;
            }

            dragData = null!;
            return false;
        }

        private void OnCardIssueSelected(object? sender, int issueId)
        {
            IssueSelected?.Invoke(this, issueId);
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
            if (TryGetDragData(e.Data, out var dragData)
                && dragData.Issue.Type != JiraClone.Domain.Enums.IssueType.Epic
                && dragData.Issue.StatusId != _column.StatusId
                && dragData.Issue.EpicId == _laneEpicId)
            {
                var isWipReached = IsWipLimitReached();
                e.Effect = DragDropEffects.Move;
                SetDropHighlight(true, isWipReached ? WipLimitHighlightColor : DropHighlightColor);
                EnsureDropPlaceholder(isWipReached ? WipLimitHighlightColor : DropHighlightColor);
                if (isWipReached && !_wipWarningRaised && _column.WipLimit.HasValue)
                {
                    _wipWarningRaised = true;
                    WipLimitWarningRequested?.Invoke(this, new BoardColumnWipLimitEventArgs(_column.StatusId, _column.Name, _column.TotalIssueCount, _column.WipLimit.Value));
                }

                if (!isWipReached)
                {
                    _wipWarningRaised = false;
                }

                return;
            }

            e.Effect = DragDropEffects.None;
            ClearDropState();
        }

        private bool IsWipLimitReached() =>
            _column.WipLimit is int wipLimit
            && wipLimit > 0
            && _column.TotalIssueCount >= wipLimit;

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
            var hasDrop = TryGetDragData(e.Data, out var dragData)
                && dragData.Issue.Type != JiraClone.Domain.Enums.IssueType.Epic
                && dragData.Issue.StatusId != _column.StatusId
                && dragData.Issue.EpicId == _laneEpicId;
            ClearDropState();
            if (hasDrop)
            {
                IssueMoveRequested?.Invoke(this, new IssueMoveRequestedEventArgs(dragData.Issue.Id, dragData.Issue.StatusId, dragData.Issue.StatusName, _column.StatusId, _column.Name));
            }
        }

        private void EnsureDropPlaceholder(Color accentColor)
        {
            _dropPlaceholder.AccentColor = accentColor;
            if (_dropPlaceholder.Parent == _issuesPanel)
            {
                _dropPlaceholder.Invalidate();
                return;
            }

            _dropPlaceholder.Width = Math.Max(248, Width - 24);
            _issuesPanel.Controls.Add(_dropPlaceholder);
            _issuesPanel.Controls.SetChildIndex(_dropPlaceholder, 0);
            _emptyState.Visible = false;
        }

        private void RemoveDropPlaceholder()
        {
            if (_dropPlaceholder.Parent != _issuesPanel)
            {
                return;
            }

            _issuesPanel.Controls.Remove(_dropPlaceholder);
            _emptyState.Visible = _column.Issues.Count == 0;
        }

        private void DetachIssueCards()
        {
            foreach (var card in _issuesPanel.Controls.OfType<IssueCardControl>())
            {
                card.IssueSelected -= OnCardIssueSelected;
            }
        }

        private void ClearDropState()
        {
            _wipWarningRaised = false;
            SetDropHighlight(false, DropHighlightColor);
            RemoveDropPlaceholder();
        }

        private void SetDropHighlight(bool highlighted, Color accentColor)
        {
            if (_dropHighlight == highlighted && _dropHighlightColor == accentColor)
            {
                return;
            }

            _dropHighlight = highlighted;
            _dropHighlightColor = accentColor;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using var fill = new SolidBrush(JiraTheme.BoardColumnBg);
            using var borderPen = new Pen(_dropHighlight ? _dropHighlightColor : JiraTheme.Border, _dropHighlight ? 2f : 1f);
            if (_dropHighlight)
            {
                borderPen.DashStyle = DashStyle.Dash;
            }

            using var path = GraphicsHelper.CreateRoundedPath(bounds, JiraTheme.BorderRadius);
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(borderPen, path);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Resize -= OnCellResize;
                DetachIssueCards();
                UnconfigureDropTarget(this);
                UnconfigureDropTarget(_bodyPanel);
                UnconfigureDropTarget(_issuesPanel);
                UnconfigureDropTarget(_emptyState);
            }

            base.Dispose(disposing);
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

        public Color AccentColor { get; set; } = ColorTranslator.FromHtml("#0052CC");

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Math.Max(0, Width - 4), Math.Max(0, Height - 4));
            using var path = GraphicsHelper.CreateRoundedPath(bounds, JiraTheme.BorderRadius);
            using var fillBrush = new SolidBrush(Color.FromArgb(28, AccentColor));
            using var borderPen = new Pen(AccentColor, 2f) { DashStyle = DashStyle.Dash };
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

public sealed record EpicSwimlaneViewModel(
    string LaneKey,
    int? EpicId,
    string DisplayTitle,
    Color HeaderColor,
    bool IsCollapsed,
    int DoneIssues,
    int TotalIssues,
    int DoneStoryPoints,
    int TotalStoryPoints,
    IReadOnlyList<EpicSwimlaneColumnViewModel> Columns)
{
    public static EpicSwimlaneViewModel Empty { get; } = new(string.Empty, null, string.Empty, JiraTheme.Neutral500, false, 0, 0, 0, 0, []);
}

public sealed record EpicSwimlaneColumnViewModel(
    int StatusId,
    string Name,
    string Color,
    int? WipLimit,
    int TotalIssueCount,
    IReadOnlyList<IssueSummaryDto> Issues);

public sealed record EpicSwimlaneCollapseChangedEventArgs(string LaneKey, bool IsCollapsed);
