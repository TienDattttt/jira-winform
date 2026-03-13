using System.Drawing.Drawing2D;
using System.Drawing.Text;
using JiraClone.Application.Models;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public class BoardColumnControl : UserControl
{
    private readonly HeaderPanel _headerPanel;
    private readonly Panel _scrollHost;
    private readonly FlowLayoutPanel _issuesPanel;
    private readonly Label _emptyState;
    private readonly LinkLabel _createIssueLink;
    private BoardColumnDto _column;

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

        Controls.Add(_scrollHost);
        Controls.Add(_createIssueLink);
        Controls.Add(_headerPanel);
        Resize += (_, _) => UpdateScrollMetrics();

        Bind(column);
    }

    public event EventHandler<int>? IssueSelected;
    public event EventHandler<JiraClone.Domain.Enums.IssueStatus>? CreateIssueRequested;
    public event EventHandler<IssueMoveRequestedEventArgs>? IssueMoveRequested;

    public void Bind(BoardColumnDto column)
    {
        _column = column;
        _headerPanel.Title = column.Name;
        _headerPanel.Count = column.Issues.Count;
        _issuesPanel.Controls.Clear();

        foreach (var issue in column.Issues)
        {
            var card = new IssueCardControl(issue);
            card.Width = Math.Max(248, Width - SystemInformation.VerticalScrollBarWidth - 12);
            card.IssueSelected += (_, issueId) => IssueSelected?.Invoke(this, issueId);
            card.IssueMoveRequested += (_, args) => IssueMoveRequested?.Invoke(this, args);
            card.Height = card.GetPreferredSize(new Size(card.Width, 0)).Height;
            _issuesPanel.Controls.Add(card);
        }

        _emptyState.Visible = column.Issues.Count == 0;
        UpdateScrollMetrics();
        _headerPanel.Invalidate();
    }

    private void UpdateScrollMetrics()
    {
        _issuesPanel.Width = Math.Max(248, ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);

        foreach (var control in _issuesPanel.Controls.OfType<IssueCardControl>())
        {
            control.Width = _issuesPanel.Width;
            control.Height = control.GetPreferredSize(new Size(control.Width, 0)).Height;
        }

        var totalHeight = _issuesPanel.Controls.Cast<Control>().Sum(x => x.Height + x.Margin.Vertical);
        if (_emptyState.Visible)
        {
            totalHeight += _emptyState.Height;
        }

        _scrollHost.AutoScrollMinSize = new Size(_issuesPanel.Width, totalHeight + JiraTheme.Md);
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

            using var badgePath = CreateRoundedPath(badgeBounds, 10);
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
    }
}

