using System.Drawing.Drawing2D;
using System.Drawing.Text;
using JiraClone.Application.Dashboard;
using JiraClone.Application.Issues;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.Persistence.Repositories;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Helpers;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public sealed class DashboardForm : UserControl
{
    private static readonly Color[] ChartPalette =
    [
        JiraTheme.Blue600,
        JiraTheme.Red500,
        JiraTheme.Green500,
        JiraTheme.Orange400,
        JiraTheme.Purple500,
        JiraTheme.Teal500,
        JiraTheme.Yellow300
    ];

    private readonly AppSession _session;
    private readonly Label _titleLabel = JiraControlFactory.CreateLabel("Dashboard");
    private readonly Label _subtitleLabel = JiraControlFactory.CreateLabel("Project overview across sprint progress, issue mix, activity, and team workload.", true);
    private readonly Label _autoRefreshLabel = JiraControlFactory.CreateLabel("Auto-refresh every 5 minutes", true);
    private readonly Button _refreshButton = JiraControlFactory.CreateSecondaryButton("Refresh");
    private readonly FlowLayoutPanel _widgetsPanel = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight,
        BackColor = JiraTheme.BgPage,
        Padding = new Padding(20, 0, 20, 20),
        Margin = new Padding(0)
    };
    private readonly DashboardWidgetCard _sprintCard = new("Sprint Progress", "No active sprint");
    private readonly DashboardWidgetCard _statisticsCard = new("Issue Statistics", "Type and priority mix for the active project");
    private readonly DashboardWidgetCard _activityCard = new("Recent Activity", "The latest 10 changes across the project");
    private readonly DashboardWidgetCard _assignedCard = new("Assigned To Me", "In-progress issues that currently need your attention");
    private readonly DashboardWidgetCard _teamCard = new("Team Workload", "Open and in-progress load by teammate");
    private readonly SprintProgressPanel _sprintPanel = new() { Dock = DockStyle.Fill };
    private readonly IssueStatisticsPanel _statisticsPanel = new() { Dock = DockStyle.Fill };
    private readonly Label _activityEmptyState = CreateEmptyState("No recent activity to show.");
    private readonly DoubleBufferedPanel _activityBody = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = JiraTheme.BgSurface };
    private readonly Label _assignedEmptyState = CreateEmptyState("You have no in-progress issues right now.");
    private readonly DoubleBufferedPanel _assignedBody = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = JiraTheme.BgSurface };
    private readonly Label _teamEmptyState = CreateEmptyState("No team members are available in the active project.");
    private readonly DoubleBufferedPanel _teamBody = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = JiraTheme.BgSurface };
    private readonly Panel _teamHeader = CreateTeamHeader();
    private readonly System.Windows.Forms.Timer _autoRefreshTimer = new() { Interval = 5 * 60 * 1000 };

    private Project? _project;
    private DashboardOverviewDto? _overview;
    private string _shellSearch = string.Empty;
    private readonly CancellationTokenSource _disposeCts = new();
    private CancellationTokenSource? _loadCts;

    public DashboardForm(AppSession session)
    {
        _session = session;
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);

        _titleLabel.Font = JiraTheme.FontH1;
        ConfigureActionButton(_refreshButton, 108);
        _refreshButton.Click += OnRefreshButtonClick;
        _autoRefreshTimer.Tick += OnAutoRefreshTimerTick;


        _sprintCard.SetBody(_sprintPanel);
        _statisticsCard.SetBody(_statisticsPanel);
        _activityCard.SetBody(BuildActivityBody());
        _assignedCard.SetBody(BuildAssignedBody());
        _teamCard.SetBody(BuildTeamBody());

        _widgetsPanel.Controls.AddRange([_sprintCard, _statisticsCard, _activityCard, _assignedCard, _teamCard]);

        Controls.Add(BuildLayout());

        Load += OnDashboardLoad;
        Resize += OnDashboardResize;
        VisibleChanged += OnDashboardVisibleChanged;
    }

    public Task RefreshDashboardAsync(CancellationToken cancellationToken = default) => ReloadDashboardAsync(cancellationToken);

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = JiraTheme.BgPage,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildContentArea(), 0, 1);
        return root;
    }

    private Control BuildContentArea()
    {
        var content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgPage,
        };
        content.Controls.Add(_widgetsPanel);
        content.Controls.Add(BuildToolbar());
        return content;
    }
    private Task ReloadDashboardAsync(CancellationToken cancellationToken = default) =>
        LoadDashboardAsync(RestartLoadCancellation(cancellationToken));

    private CancellationToken RestartLoadCancellation(CancellationToken cancellationToken = default)
    {
        CancelPendingLoad();
        _loadCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, cancellationToken);
        return _loadCts.Token;
    }

    private void CancelPendingLoad()
    {
        if (_loadCts is null)
        {
            return;
        }

        try
        {
            _loadCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _loadCts.Dispose();
        _loadCts = null;
    }


    public void SetShellSearch(string value)
    {
        _shellSearch = value?.Trim() ?? string.Empty;
        BindOverview();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelPendingLoad();
            _disposeCts.Cancel();
            Load -= OnDashboardLoad;
            Resize -= OnDashboardResize;
            VisibleChanged -= OnDashboardVisibleChanged;
            _refreshButton.Click -= OnRefreshButtonClick;
            _autoRefreshTimer.Tick -= OnAutoRefreshTimerTick;
            _autoRefreshTimer.Stop();
            _autoRefreshTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 104,
            BackColor = JiraTheme.BgPage,
            Padding = new Padding(20, 18, 20, 8)
        };

        _titleLabel.Location = new Point(0, 0);
        _subtitleLabel.Location = new Point(0, 42);
        header.Controls.Add(_titleLabel);
        header.Controls.Add(_subtitleLabel);
        return header;
    }

    private Control BuildToolbar()
    {
        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = JiraTheme.BgPage,
            Padding = new Padding(20, 0, 20, 12)
        };

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = JiraTheme.BgPage,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _autoRefreshLabel.Margin = new Padding(0, 10, 0, 0);
        left.Controls.Add(_autoRefreshLabel);

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = JiraTheme.BgPage,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        right.Controls.Add(_refreshButton);

        toolbar.Controls.Add(right);
        toolbar.Controls.Add(left);
        return toolbar;
    }

    private Control BuildActivityBody()
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
        host.Controls.Add(_activityEmptyState);
        host.Controls.Add(_activityBody);
        return host;
    }

    private Control BuildAssignedBody()
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
        host.Controls.Add(_assignedEmptyState);
        host.Controls.Add(_assignedBody);
        return host;
    }

    private Control BuildTeamBody()
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
        host.Controls.Add(_teamEmptyState);
        host.Controls.Add(_teamBody);
        host.Controls.Add(_teamHeader);
        return host;
    }

    private async Task LoadDashboardAsync(CancellationToken cancellationToken = default)
    {
        if (!Visible)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {            SetBusyState(true);

            Project? project = null;
            DashboardOverviewDto? overview = null;

            await _session.RunSerializedAsync(async () =>
            {
                project = await _session.Projects.GetActiveProjectAsync(cancellationToken);
                if (project is null)
                {
                    return;
                }

                overview = await _session.Dashboard.GetOverviewAsync(project.Id, cancellationToken);
            }, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            _project = project;
            _overview = overview;
            _subtitleLabel.Text = project is null
                ? "Choose an active project to see sprint health, activity, and workload."
                : $"Overview for {project.Name}. Search filters activity, assigned work, and team rows.";

            BindOverview();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException) when (_disposeCts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
        finally
        {            SetBusyState(false);
        }
    }


    private void BindOverview()
    {
        if (_project is null || _overview is null)
        {
            ApplyNoProjectState();
            return;
        }

        ApplySprintData(_overview.SprintProgress);
        _statisticsPanel.Data = _overview.IssueStatistics;
        BindActivity(_overview.RecentActivities);
        BindAssignedIssues(_overview.AssignedToMe);
        BindTeamWorkload(_overview.TeamWorkload);
    }

    private void ApplyNoProjectState()
    {
        _sprintCard.SetSubtitle("No active project");
        _sprintPanel.Data = null;

        _statisticsCard.SetSubtitle("No active project");
        _statisticsPanel.Data = null;

        _activityCard.SetSubtitle("No active project");
        _activityBody.Controls.Clear();
        _activityEmptyState.Text = "Choose an active project to see the latest changes.";
        _activityEmptyState.Visible = true;

        _assignedCard.SetSubtitle("No active project");
        _assignedBody.Controls.Clear();
        _assignedEmptyState.Text = "Choose an active project to see your assigned work.";
        _assignedEmptyState.Visible = true;

        _teamCard.SetSubtitle("No active project");
        _teamBody.Controls.Clear();
        _teamHeader.Visible = false;
        _teamEmptyState.Text = "Choose an active project to see team workload.";
        _teamEmptyState.Visible = true;
    }

    private void ApplySprintData(DashboardSprintProgressDto data)
    {
        if (!data.HasActiveSprint || string.IsNullOrWhiteSpace(data.SprintName))
        {
            _sprintCard.SetSubtitle("No active sprint");
            _sprintPanel.Data = data;
            return;
        }

        var range = data.StartDate.HasValue && data.EndDate.HasValue
            ? $"{data.StartDate:dd MMM} - {data.EndDate:dd MMM yyyy}"
            : "Sprint dates pending";
        _sprintCard.SetSubtitle($"{data.SprintName} | {range}");
        _sprintPanel.Data = data;
    }

    private void BindActivity(IReadOnlyList<DashboardActivityDto> allActivities)
    {
        var filtered = allActivities
            .Where(activity => string.IsNullOrWhiteSpace(_shellSearch)
                || FormatActivitySearchText(activity).Contains(_shellSearch, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _activityCard.SetSubtitle(filtered.Count == 0
            ? "No recent activity matches the current filter"
            : $"Showing {filtered.Count} recent activit{(filtered.Count == 1 ? "y" : "ies")}");

        RebuildStack(
            _activityBody,
            filtered.Select(activity =>
            {
                var row = new ActivityRowControl(activity, FormatActivityText(activity), FormatTimeAgo(activity.OccurredAtUtc));
                row.IssueRequested += HandleIssueRequested;
                return row;
            }).Cast<Control>().ToList(),
            _activityEmptyState,
            filtered.Count == 0 ? "No recent activity matches the current filter." : "No recent activity to show.");
    }

    private void BindAssignedIssues(IReadOnlyList<DashboardIssueDto> allIssues)
    {
        var filtered = allIssues
            .Where(issue => string.IsNullOrWhiteSpace(_shellSearch)
                || $"{issue.IssueKey} {issue.Title} {issue.Type} {issue.Priority}".Contains(_shellSearch, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _assignedCard.SetSubtitle(filtered.Count == 0
            ? "Nothing in progress matches the current filter"
            : $"{filtered.Count} in-progress issue{(filtered.Count == 1 ? string.Empty : "s")} assigned to you");

        RebuildStack(
            _assignedBody,
            filtered.Select(issue =>
            {
                var row = new AssignedIssueRowControl(issue);
                row.IssueRequested += HandleIssueRequested;
                return row;
            }).Cast<Control>().ToList(),
            _assignedEmptyState,
            filtered.Count == 0 ? "No assigned in-progress issues match the current filter." : "You have no in-progress issues right now.");
    }

    private void BindTeamWorkload(IReadOnlyList<DashboardTeamWorkloadDto> allRows)
    {
        var filtered = allRows
            .Where(row => string.IsNullOrWhiteSpace(_shellSearch)
                || row.DisplayName.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _teamCard.SetSubtitle(filtered.Count == 0
            ? "No team members match the current filter"
            : $"{filtered.Count} teammate{(filtered.Count == 1 ? string.Empty : "s")} in the active project");
        _teamHeader.Visible = filtered.Count > 0;

        RebuildStack(
            _teamBody,
            filtered.Select(row => (Control)new TeamWorkloadRowControl(row)).ToList(),
            _teamEmptyState,
            filtered.Count == 0 ? "No team members match the current filter." : "No team members are available in the active project.",
            topOffset: _teamHeader.Height);
    }

    private void RebuildStack(DoubleBufferedPanel body, IReadOnlyList<Control> rows, Label emptyState, string emptyText, int topOffset = 0)
    {
        body.SuspendLayout();
        try
        {
            foreach (Control control in body.Controls)
            {
                control.Dispose();
            }

            body.Controls.Clear();
            body.Padding = new Padding(0, topOffset, 0, 0);

            if (rows.Count == 0)
            {
                emptyState.Text = emptyText;
                emptyState.Visible = true;
                return;
            }

            emptyState.Visible = false;
            for (var index = rows.Count - 1; index >= 0; index--)
            {
                rows[index].Dock = DockStyle.Top;
                body.Controls.Add(rows[index]);
            }
        }
        finally
        {
            body.ResumeLayout();
        }
    }

    private async void OnDashboardLoad(object? sender, EventArgs e)
    {
        ApplyResponsiveLayout();
        _autoRefreshTimer.Start();
        await ReloadDashboardAsync(_disposeCts.Token);
    }

    private void OnDashboardResize(object? sender, EventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void OnDashboardVisibleChanged(object? sender, EventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        _autoRefreshTimer.Enabled = Visible;
    }

    private async void OnRefreshButtonClick(object? sender, EventArgs e)
    {
        await ReloadDashboardAsync(_disposeCts.Token);
    }

    private async void OnAutoRefreshTimerTick(object? sender, EventArgs e)
    {
        await ReloadDashboardAsync(_disposeCts.Token);
    }

    private async void HandleIssueRequested(object? sender, int issueId)
    {
        if (_project is null || IsDisposed)
        {
            return;
        }

        try
        {
            using var dialog = new IssueDetailsForm(_session, issueId, _project.Id);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                await ReloadDashboardAsync(_disposeCts.Token);
            }
        }
        catch (OperationCanceledException) when (_disposeCts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private void ApplyResponsiveLayout()
    {
        var availableWidth = Math.Max(320, _widgetsPanel.ClientSize.Width - _widgetsPanel.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
        var halfWidth = Math.Max(360, (availableWidth - 16) / 2);
        var useSingleColumn = availableWidth < 980;
        var fullWidth = availableWidth;

        _sprintCard.Width = useSingleColumn ? fullWidth : halfWidth;
        _statisticsCard.Width = useSingleColumn ? fullWidth : halfWidth;
        _activityCard.Width = useSingleColumn ? fullWidth : halfWidth;
        _assignedCard.Width = useSingleColumn ? fullWidth : halfWidth;
        _teamCard.Width = fullWidth;

        _sprintCard.Height = 250;
        _statisticsCard.Height = 320;
        _activityCard.Height = 360;
        _assignedCard.Height = 320;
        _teamCard.Height = 320;

        foreach (DashboardWidgetCard card in new[] { _sprintCard, _statisticsCard, _activityCard, _assignedCard, _teamCard })
        {
            card.Margin = new Padding(0, 0, 16, 16);
        }

        _teamCard.Margin = new Padding(0, 0, 0, 16);
    }

    private void SetBusyState(bool isBusy)
    {
        UseWaitCursor = isBusy;
        _refreshButton.Enabled = !isBusy;
    }

    private static Label CreateEmptyState(string text)
    {
        var label = JiraControlFactory.CreateLabel(text, true);
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleCenter;
        label.Visible = false;
        return label;
    }

    private static Panel CreateTeamHeader()
    {
        var header = new TeamHeaderPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(16, 8, 16, 6)
        };

        header.Controls.Add(CreateHeaderLabel("In Progress", 520, 120, ContentAlignment.MiddleRight));
        header.Controls.Add(CreateHeaderLabel("Open", 392, 90, ContentAlignment.MiddleRight));
        header.Controls.Add(CreateHeaderLabel("Name", 64, 250, ContentAlignment.MiddleLeft));
        header.Controls.Add(CreateHeaderLabel("Avatar", 16, 42, ContentAlignment.MiddleLeft));
        return header;
    }

    private static Label CreateHeaderLabel(string text, int left, int width, ContentAlignment alignment)

    {
        var label = JiraControlFactory.CreateLabel(text, true);
        label.AutoSize = false;
        label.TextAlign = alignment;
        label.Location = new Point(left, 0);
        label.Size = new Size(width, 18);
        return label;
    }

    private static void ConfigureActionButton(Button button, int width)
    {
        button.AutoSize = false;
        button.Width = width;
        button.Height = 40;
        button.MinimumSize = new Size(width, 36);
    }

    private static string FormatActivityText(DashboardActivityDto activity)
    {
        var issuePart = string.IsNullOrWhiteSpace(activity.IssueKey) ? "an issue" : activity.IssueKey;
        return activity.ActionType switch
        {
            ActivityActionType.Created => $"{activity.UserDisplayName} created {issuePart}",
            ActivityActionType.Updated => $"{activity.UserDisplayName} updated {activity.FieldName ?? "details"} on {issuePart}",
            ActivityActionType.Deleted => $"{activity.UserDisplayName} deleted {issuePart}",
            ActivityActionType.StatusChanged => $"{activity.UserDisplayName} moved {issuePart} to {activity.NewValue ?? "a new status"}",
            ActivityActionType.CommentAdded => $"{activity.UserDisplayName} commented on {issuePart}",
            ActivityActionType.CommentUpdated => $"{activity.UserDisplayName} edited a comment on {issuePart}",
            ActivityActionType.CommentDeleted => $"{activity.UserDisplayName} removed a comment on {issuePart}",
            ActivityActionType.AttachmentAdded => $"{activity.UserDisplayName} added an attachment to {issuePart}",
            ActivityActionType.AttachmentRemoved => $"{activity.UserDisplayName} removed an attachment from {issuePart}",
            ActivityActionType.SprintAssigned => $"{activity.UserDisplayName} assigned {issuePart} to sprint {activity.NewValue ?? "backlog"}",
            ActivityActionType.SprintClosed => $"{activity.UserDisplayName} closed sprint {activity.NewValue ?? string.Empty}".TrimEnd(),
            _ => $"{activity.UserDisplayName} changed {issuePart}"
        };
    }

    private static string FormatActivitySearchText(DashboardActivityDto activity) =>
        $"{activity.UserDisplayName} {activity.IssueKey} {activity.ActionType} {activity.FieldName} {activity.NewValue} {activity.OldValue}";

    private static string FormatTimeAgo(DateTime utcTimestamp)
    {
        var delta = DateTime.UtcNow - utcTimestamp.ToUniversalTime();
        if (delta.TotalSeconds < 60)
        {
            return "just now";
        }

        if (delta.TotalMinutes < 60)
        {
            var minutes = Math.Max(1, (int)Math.Floor(delta.TotalMinutes));
            return $"{minutes} minute{(minutes == 1 ? string.Empty : "s")} ago";
        }

        if (delta.TotalHours < 24)
        {
            var hours = Math.Max(1, (int)Math.Floor(delta.TotalHours));
            return $"{hours} hour{(hours == 1 ? string.Empty : "s")} ago";
        }

        if (delta.TotalDays < 7)
        {
            var days = Math.Max(1, (int)Math.Floor(delta.TotalDays));
            return $"{days} day{(days == 1 ? string.Empty : "s")} ago";
        }

        return utcTimestamp.ToLocalTime().ToString("dd MMM yyyy");
    }

    private static string BuildInitials(string text)
    {
        var parts = (text ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => char.ToUpperInvariant(part[0]))
            .Take(2)
            .ToArray();
        return parts.Length == 0 ? "?" : new string(parts);
    }

    private static Color ResolveAvatarColor(string text)
    {
        var palette = new[]
        {
            JiraTheme.Blue600,
            JiraTheme.Teal500,
            JiraTheme.Green500,
            JiraTheme.Orange400,
            JiraTheme.Purple500,
            JiraTheme.Red500
        };
        return palette[Math.Abs((text ?? string.Empty).GetHashCode()) % palette.Length];
    }

    private sealed class DashboardWidgetCard : Panel
    {
        private readonly Label _titleLabel = JiraControlFactory.CreateLabel(string.Empty);
        private readonly Label _subtitleLabel = JiraControlFactory.CreateLabel(string.Empty, true);
        private readonly Panel _bodyHost = new() { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };

        public DashboardWidgetCard(string title, string subtitle)
        {
            DoubleBuffered = true;
            BackColor = JiraTheme.BgSurface;
            Padding = new Padding(18, 16, 18, 18);

            _titleLabel.Font = JiraTheme.FontH2;
            _titleLabel.Location = new Point(0, 0);
            _subtitleLabel.Location = new Point(0, 34);
            _subtitleLabel.MaximumSize = new Size(720, 32);

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 62,
                BackColor = JiraTheme.BgSurface
            };
            header.Controls.Add(_titleLabel);
            header.Controls.Add(_subtitleLabel);

            Controls.Add(_bodyHost);
            Controls.Add(header);

            SetTitle(title);
            SetSubtitle(subtitle);
        }

        public void SetTitle(string value) => _titleLabel.Text = value;

        public void SetSubtitle(string value) => _subtitleLabel.Text = value;

        public void SetBody(Control body)
        {
            _bodyHost.Controls.Clear();
            _bodyHost.Controls.Add(body);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = GraphicsHelper.CreateRoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 10);
            using var background = new SolidBrush(JiraTheme.BgSurface);
            using var border = new Pen(JiraTheme.Border);
            e.Graphics.FillPath(background, path);
            e.Graphics.DrawPath(border, path);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            using var path = GraphicsHelper.CreateRoundedPath(new Rectangle(0, 0, Width, Height), 10);
            Region = new Region(path);
        }
    }

    private sealed class SprintProgressPanel : Panel
    {
        private DashboardSprintProgressDto? _data;

        public SprintProgressPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = JiraTheme.BgSurface;
        }

        public DashboardSprintProgressDto? Data
        {
            get => _data;
            set
            {
                _data = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            if (_data is null)
            {
                DrawEmptyState(e.Graphics, "Choose an active project to see sprint progress.");
                return;
            }

            if (!_data.HasActiveSprint)
            {
                DrawEmptyState(e.Graphics, "No active sprint is running for this project.");
                return;
            }

            var storyPointsTotal = Math.Max(0, _data.TotalStoryPoints);
            var storyPointsDone = Math.Max(0, _data.DoneStoryPoints);
            var progress = storyPointsTotal == 0 ? 0d : Math.Clamp(storyPointsDone / (double)storyPointsTotal, 0d, 1d);
            var issueSummary = $"{_data.DoneIssues} / {_data.TotalIssues} issues done";
            var pointSummary = storyPointsTotal == 0
                ? "No story points committed yet"
                : $"{storyPointsDone} / {storyPointsTotal} story points complete";

            TextRenderer.DrawText(e.Graphics, pointSummary, JiraTheme.FontBody, new Rectangle(0, 0, Width, 24), JiraTheme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics, issueSummary, JiraTheme.FontCaption, new Rectangle(0, 26, Width, 18), JiraTheme.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            var barBounds = new Rectangle(0, 58, Math.Max(60, Width - 4), 18);
            using var barPath = GraphicsHelper.CreateRoundedPath(barBounds, 9);
            using var trackBrush = new SolidBrush(JiraTheme.Neutral200);
            using var fillBrush = new LinearGradientBrush(barBounds, JiraTheme.Blue600, JiraTheme.Green500, LinearGradientMode.Horizontal);
            e.Graphics.FillPath(trackBrush, barPath);
            if (progress > 0)
            {
                var fillWidth = Math.Max(12, (int)Math.Round(barBounds.Width * progress));
                using var fillPath = GraphicsHelper.CreateRoundedPath(new Rectangle(barBounds.X, barBounds.Y, Math.Min(barBounds.Width, fillWidth), barBounds.Height), 9);
                e.Graphics.FillPath(fillBrush, fillPath);
            }

            var breakdownTop = 104;
            var chips = _data.StatusCounts
                .OrderBy(x => x.Category)
                .ThenBy(x => x.StatusName)
                .ToList();
            var left = 0;
            foreach (var chip in chips)
            {
                var color = ParseColor(chip.Color, chip.Category);
                var text = $"{chip.StatusName}: {chip.Count}";
                var chipSize = TextRenderer.MeasureText(text, JiraTheme.FontCaption);
                var chipBounds = new Rectangle(left, breakdownTop, Math.Max(94, chipSize.Width + 24), 24);
                using var chipPath = GraphicsHelper.CreateRoundedPath(chipBounds, 12);
                using var chipBrush = new SolidBrush(Color.FromArgb(32, color));
                using var dotBrush = new SolidBrush(color);
                e.Graphics.FillPath(chipBrush, chipPath);
                e.Graphics.FillEllipse(dotBrush, chipBounds.X + 8, chipBounds.Y + 8, 8, 8);
                TextRenderer.DrawText(e.Graphics, text, JiraTheme.FontCaption, new Rectangle(chipBounds.X + 22, chipBounds.Y + 2, chipBounds.Width - 26, chipBounds.Height - 4), JiraTheme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                left += chipBounds.Width + 10;
            }
        }

        private static void DrawEmptyState(Graphics graphics, string text)
        {
            TextRenderer.DrawText(graphics, text, JiraTheme.FontBody, new Rectangle(0, 0, 480, 90), JiraTheme.TextSecondary, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);
        }
    }

    private sealed class IssueStatisticsPanel : Panel
    {
        private DashboardIssueStatisticsDto? _data;

        public IssueStatisticsPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = JiraTheme.BgSurface;
        }

        public DashboardIssueStatisticsDto? Data
        {
            get => _data;
            set
            {
                _data = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            if (_data is null)
            {
                TextRenderer.DrawText(e.Graphics, "Choose an active project to see issue mix.", JiraTheme.FontBody, ClientRectangle, JiraTheme.TextSecondary, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);
                return;
            }

            var halfWidth = Math.Max(180, Width / 2);
            DrawDonutSection(e.Graphics, new Rectangle(0, 0, halfWidth, Height), "By Type", _data.TypeBreakdown);
            DrawDonutSection(e.Graphics, new Rectangle(halfWidth, 0, Width - halfWidth, Height), "By Priority", _data.PriorityBreakdown);
        }

        private static void DrawDonutSection(Graphics graphics, Rectangle bounds, string title, IReadOnlyList<DashboardChartSliceDto> slices)
        {
            TextRenderer.DrawText(graphics, title, JiraTheme.FontBody, new Rectangle(bounds.X, bounds.Y, bounds.Width, 22), JiraTheme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            if (slices.Count == 0)
            {
                TextRenderer.DrawText(graphics, "No issue data", JiraTheme.FontCaption, new Rectangle(bounds.X, bounds.Y + 30, bounds.Width, 22), JiraTheme.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                return;
            }

            var donutSize = Math.Min(140, Math.Max(96, bounds.Width - 64));
            var donutBounds = new Rectangle(bounds.X + 12, bounds.Y + 38, donutSize, donutSize);
            var total = Math.Max(1, slices.Sum(slice => slice.Value));
            var startAngle = -90f;
            var innerInset = 26;

            for (var index = 0; index < slices.Count; index++)
            {
                var slice = slices[index];
                var sweep = 360f * (slice.Value / (float)total);
                using var brush = new SolidBrush(ChartPalette[index % ChartPalette.Length]);
                graphics.FillPie(brush, donutBounds, startAngle, sweep);
                startAngle += sweep;
            }

            var innerBounds = Rectangle.Inflate(donutBounds, -innerInset, -innerInset);
            using var innerBrush = new SolidBrush(JiraTheme.BgSurface);
            graphics.FillEllipse(innerBrush, innerBounds);

            TextRenderer.DrawText(graphics, total.ToString(), JiraTheme.FontH2, innerBounds, JiraTheme.TextPrimary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            var legendTop = donutBounds.Bottom + 12;
            for (var index = 0; index < slices.Count; index++)
            {
                var slice = slices[index];
                using var colorBrush = new SolidBrush(ChartPalette[index % ChartPalette.Length]);
                graphics.FillRectangle(colorBrush, bounds.X + 12, legendTop + (index * 20) + 4, 10, 10);
                TextRenderer.DrawText(
                    graphics,
                    $"{slice.Label} ({slice.Value})",
                    JiraTheme.FontCaption,
                    new Rectangle(bounds.X + 28, legendTop + (index * 20), bounds.Width - 40, 18),
                    JiraTheme.TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }

    private sealed class TeamHeaderPanel : Panel
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }

    private sealed class ActivityRowControl : Control
    {
        private readonly DashboardActivityDto _activity;
        private readonly string _text;
        private readonly string _timeAgo;
        private bool _hovered;

        public ActivityRowControl(DashboardActivityDto activity, string text, string timeAgo)
        {
            _activity = activity;
            _text = text;
            _timeAgo = timeAgo;
            Height = 58;
            Cursor = activity.IssueId.HasValue ? Cursors.Hand : Cursors.Default;
            BackColor = JiraTheme.BgSurface;
            DoubleBuffered = true;
            Margin = Padding.Empty;
            Padding = Padding.Empty;
        }

        public event EventHandler<int>? IssueRequested;

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (_activity.IssueId.HasValue)
            {
                IssueRequested?.Invoke(this, _activity.IssueId.Value);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var rowBounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using var background = new SolidBrush(_hovered ? JiraTheme.Neutral100 : JiraTheme.BgSurface);
            using var divider = new Pen(JiraTheme.Border);
            e.Graphics.FillRectangle(background, rowBounds);
            e.Graphics.DrawLine(divider, 0, Height - 1, Width, Height - 1);

            DrawAvatar(e.Graphics, new Rectangle(14, 12, 32, 32), _activity.UserDisplayName);
            TextRenderer.DrawText(e.Graphics, _text, JiraTheme.FontSmall, new Rectangle(58, 10, Width - 72, 24), JiraTheme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, _timeAgo, JiraTheme.FontCaption, new Rectangle(58, 30, Width - 72, 18), JiraTheme.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private sealed class AssignedIssueRowControl : Control
    {
        private readonly DashboardIssueDto _issue;
        private bool _hovered;

        public AssignedIssueRowControl(DashboardIssueDto issue)
        {
            _issue = issue;
            Height = 66;
            Cursor = Cursors.Hand;
            BackColor = JiraTheme.BgSurface;
            DoubleBuffered = true;
            Margin = Padding.Empty;
        }

        public event EventHandler<int>? IssueRequested;

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            IssueRequested?.Invoke(this, _issue.Id);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using var background = new SolidBrush(_hovered ? JiraTheme.Neutral100 : JiraTheme.BgSurface);
            using var divider = new Pen(JiraTheme.Border);
            e.Graphics.FillRectangle(background, bounds);
            e.Graphics.DrawLine(divider, 0, Height - 1, Width, Height - 1);

            TextRenderer.DrawText(e.Graphics, _issue.IssueKey, JiraTheme.FontCaption, new Rectangle(14, 10, 96, 18), JiraTheme.PrimaryActive, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics, _issue.Title, JiraTheme.FontSmall, new Rectangle(14, 28, Width - 28, 20), JiraTheme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            DrawPill(e.Graphics, new Rectangle(14, 50, 74, 18), _issue.Type.ToString(), ResolveTypeColor(_issue.Type));
            DrawPill(e.Graphics, new Rectangle(96, 50, 88, 18), _issue.Priority.ToString(), ResolvePriorityColor(_issue.Priority));

            var meta = _issue.StoryPoints.HasValue
                ? $"{_issue.StoryPoints.Value} pts"
                : "No estimate";
            TextRenderer.DrawText(e.Graphics, meta, JiraTheme.FontCaption, new Rectangle(Width - 90, 48, 76, 18), JiraTheme.TextSecondary, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
    }

    private sealed class TeamWorkloadRowControl : Control
    {
        private readonly DashboardTeamWorkloadDto _row;

        public TeamWorkloadRowControl(DashboardTeamWorkloadDto row)
        {
            _row = row;
            Height = 44;
            BackColor = JiraTheme.BgSurface;
            DoubleBuffered = true;
            Margin = Padding.Empty;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using var divider = new Pen(JiraTheme.Border);
            e.Graphics.DrawLine(divider, 0, Height - 1, Width, Height - 1);

            DrawAvatar(e.Graphics, new Rectangle(16, 6, 28, 28), _row.DisplayName);
            TextRenderer.DrawText(e.Graphics, _row.DisplayName, JiraTheme.FontSmall, new Rectangle(64, 10, Math.Max(120, Width - 300), 20), JiraTheme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            DrawNumericPill(e.Graphics, new Rectangle(Math.Max(Width - 220, 320), 9, 70, 24), _row.OpenIssues.ToString(), JiraTheme.Neutral200, JiraTheme.TextPrimary);
            DrawNumericPill(e.Graphics, new Rectangle(Math.Max(Width - 120, 420), 9, 90, 24), _row.InProgressIssues.ToString(), JiraTheme.Blue100, JiraTheme.PrimaryActive);
        }
    }

    private sealed class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    private static void DrawAvatar(Graphics graphics, Rectangle bounds, string text)
    {
        using var brush = new SolidBrush(ResolveAvatarColor(text));
        graphics.FillEllipse(brush, bounds);
        TextRenderer.DrawText(graphics, BuildInitials(text), JiraTheme.FontCaption, bounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static void DrawPill(Graphics graphics, Rectangle bounds, string text, Color color)
    {
        using var path = GraphicsHelper.CreateRoundedPath(bounds, bounds.Height / 2);
        using var background = new SolidBrush(Color.FromArgb(28, color));
        graphics.FillPath(background, path);
        TextRenderer.DrawText(graphics, text, JiraTheme.FontCaption, bounds, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static void DrawNumericPill(Graphics graphics, Rectangle bounds, string text, Color backgroundColor, Color textColor)
    {
        using var path = GraphicsHelper.CreateRoundedPath(bounds, bounds.Height / 2);
        using var background = new SolidBrush(backgroundColor);
        graphics.FillPath(background, path);
        TextRenderer.DrawText(graphics, text, JiraTheme.FontCaption, bounds, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static Color ResolveTypeColor(IssueType type) => type switch
    {
        IssueType.Bug => JiraTheme.Red500,
        IssueType.Story => JiraTheme.Green700,
        IssueType.Epic => JiraTheme.Purple500,
        IssueType.Subtask => JiraTheme.Teal500,
        _ => JiraTheme.Blue600
    };

    private static Color ResolvePriorityColor(IssuePriority priority) => priority switch
    {
        IssuePriority.Highest => JiraTheme.Red700,
        IssuePriority.High => JiraTheme.Red500,
        IssuePriority.Medium => JiraTheme.Orange400,
        IssuePriority.Low => JiraTheme.Green700,
        _ => JiraTheme.TextSecondary
    };

    private static Color ParseColor(string? colorHex, StatusCategory category)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(colorHex))
            {
                return ColorTranslator.FromHtml(colorHex);
            }
        }
        catch
        {
        }

        return category switch
        {
            StatusCategory.Done => JiraTheme.Green500,
            StatusCategory.InProgress => JiraTheme.Blue600,
            _ => JiraTheme.Neutral500
        };
    }
}









