using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Helpers;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public sealed class RoadmapForm : UserControl
{
    private readonly AppSession _session;
    private readonly Label _titleLabel = JiraControlFactory.CreateLabel("Roadmap");
    private readonly Label _subtitleLabel = JiraControlFactory.CreateLabel("Epic timeline across the active project. Filter by sprint or assignee, zoom the horizon, and drag bars to update schedule.", true);
    private readonly ComboBox _sprintFilter = CreateFilterCombo(190);
    private readonly ComboBox _assigneeFilter = CreateFilterCombo(190);
    private readonly Button _refreshButton = JiraControlFactory.CreateSecondaryButton("Refresh");
    private readonly Label _zoomLabel = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly ListBox _epicList = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 56, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody, IntegralHeight = false };
    private readonly RoadmapTimelineCanvas _timelineCanvas = new() { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
    private readonly Label _emptyState = JiraControlFactory.CreateLabel("No epics match the current roadmap filters.", true);
    private readonly Panel _detailPanel = new() { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface, Padding = new Padding(16, 12, 16, 12) };
    private readonly Label _detailTitle = JiraControlFactory.CreateLabel("Select an epic");
    private readonly Label _detailMeta = JiraControlFactory.CreateLabel("Click an epic bar to inspect child issues and progress.", true);
    private readonly ProgressBar _detailProgress = new() { Dock = DockStyle.Top, Height = 14, Style = ProgressBarStyle.Continuous };
    private readonly Label _detailProgressCaption = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly Button _openEpicButton = JiraControlFactory.CreateSecondaryButton("Open Epic");
    private readonly Panel _detailActionHost = new() { Dock = DockStyle.Top, Height = 40, BackColor = JiraTheme.BgSurface };
    private readonly ListView _childIssuesList = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable, HideSelection = false, MultiSelect = false };
    private readonly Label _childIssuesEmpty = JiraControlFactory.CreateLabel("This epic has no linked child issues yet.", true);

    private Project? _project;
    private IReadOnlyList<RoadmapEpicDto> _allEpics = Array.Empty<RoadmapEpicDto>();
    private IReadOnlyList<RoadmapEpicDto> _filteredEpics = Array.Empty<RoadmapEpicDto>();
    private IReadOnlyList<Sprint> _sprints = Array.Empty<Sprint>();
    private IReadOnlyList<User> _users = Array.Empty<User>();
    private string _shellSearch = string.Empty;
    private int? _selectedEpicId;
    private bool _loading;
    private bool _suppressSelectionChanged;
    private int _detailRequestVersion;

    public RoadmapForm(AppSession session)
    {
        _session = session;
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;

        _titleLabel.Font = JiraTheme.FontH1;
        _refreshButton.AutoSize = false;
        _refreshButton.Size = new Size(104, 36);
        _zoomLabel.Margin = new Padding(0, 10, 0, 0);
        _sprintFilter.DisplayMember = nameof(FilterOption.Label);
        _sprintFilter.ValueMember = nameof(FilterOption.Id);
        _assigneeFilter.DisplayMember = nameof(FilterOption.Label);
        _assigneeFilter.ValueMember = nameof(FilterOption.Id);
        _emptyState.Dock = DockStyle.Fill;
        _emptyState.TextAlign = ContentAlignment.MiddleCenter;
        _emptyState.Visible = false;
        JiraTheme.StyleListView(_childIssuesList);
        _childIssuesList.Columns.Add("Key", 90, HorizontalAlignment.Left);
        _childIssuesList.Columns.Add("Title", 240, HorizontalAlignment.Left);
        _childIssuesList.Columns.Add("Status", 110, HorizontalAlignment.Left);
        _childIssuesList.Columns.Add("SP", 50, HorizontalAlignment.Right);
        _childIssuesEmpty.Dock = DockStyle.Fill;
        _childIssuesEmpty.TextAlign = ContentAlignment.MiddleCenter;
        _childIssuesEmpty.Visible = false;
        _detailTitle.Font = JiraTheme.FontH2;
        _detailProgressCaption.Dock = DockStyle.Top;
        _openEpicButton.AutoSize = false;
        _openEpicButton.Size = new Size(104, 34);
        _detailPanel.Controls.Add(BuildChildIssuesHost());
        _detailPanel.Controls.Add(BuildDetailHeader());
        Controls.Add(BuildBody());
        Controls.Add(BuildToolbar());
        Controls.Add(BuildHeader());
        _refreshButton.Click += OnRefreshButtonClick;
        _sprintFilter.SelectedIndexChanged += OnFilterChanged;
        _assigneeFilter.SelectedIndexChanged += OnFilterChanged;
        _epicList.DrawItem += OnEpicListDrawItem;
        _epicList.SelectedIndexChanged += OnEpicListSelectedIndexChanged;
        _timelineCanvas.EpicClicked += OnTimelineEpicClicked;
        _timelineCanvas.EpicDoubleClicked += OnTimelineEpicDoubleClicked;
        _timelineCanvas.ScheduleChanged += OnTimelineScheduleChanged;
        _timelineCanvas.ZoomChanged += OnTimelineZoomChanged;
        _openEpicButton.Click += OnOpenEpicButtonClick;
        _detailActionHost.Resize += OnDetailActionHostResize;
        _childIssuesList.DoubleClick += OnChildIssuesListDoubleClick;
        Load += OnRoadmapFormLoad;
        UpdateZoomLabel();
    }

    public async Task RefreshRoadmapAsync(CancellationToken cancellationToken = default)
    {
        if (_loading) return;
        try
        {
            _loading = true;
            _refreshButton.Enabled = false;
            Project? project = null;
            IReadOnlyList<RoadmapEpicDto> epics = Array.Empty<RoadmapEpicDto>();
            IReadOnlyList<Sprint> sprints = Array.Empty<Sprint>();
            IReadOnlyList<User> users = Array.Empty<User>();
            await _session.RunSerializedAsync(async () =>
            {
                project = await _session.Projects.GetActiveProjectAsync(cancellationToken);
                if (project is null) return;
                epics = await _session.Roadmap.GetEpicsForRoadmapAsync(project.Id, cancellationToken);
                sprints = await _session.Sprints.GetByProjectAsync(project.Id, cancellationToken);
                users = await _session.Users.GetProjectUsersAsync(project.Id, cancellationToken);
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _project = project;
            _allEpics = epics;
            _sprints = sprints;
            _users = users;
            _subtitleLabel.Text = project is null ? "Choose an active project to see the roadmap." : $"Timeline for {project.Name}. Use the sidebar to jump between epics and drag bars to update dates.";
            BindFilterChoices();
            ApplyFilters(_selectedEpicId);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
        finally
        {
            _loading = false;
            _refreshButton.Enabled = true;
        }
    }

    public void SetShellSearch(string value)
    {
        _shellSearch = value?.Trim() ?? string.Empty;
        ApplyFilters(_selectedEpicId);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshButton.Click -= OnRefreshButtonClick;
            _sprintFilter.SelectedIndexChanged -= OnFilterChanged;
            _assigneeFilter.SelectedIndexChanged -= OnFilterChanged;
            _epicList.DrawItem -= OnEpicListDrawItem;
            _epicList.SelectedIndexChanged -= OnEpicListSelectedIndexChanged;
            _timelineCanvas.EpicClicked -= OnTimelineEpicClicked;
            _timelineCanvas.EpicDoubleClicked -= OnTimelineEpicDoubleClicked;
            _timelineCanvas.ScheduleChanged -= OnTimelineScheduleChanged;
            _timelineCanvas.ZoomChanged -= OnTimelineZoomChanged;
            _openEpicButton.Click -= OnOpenEpicButtonClick;
            _detailActionHost.Resize -= OnDetailActionHostResize;
            _childIssuesList.DoubleClick -= OnChildIssuesListDoubleClick;
            Load -= OnRoadmapFormLoad;
        }
        base.Dispose(disposing);
    }

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = JiraTheme.BgPage, Padding = new Padding(20, 18, 20, 8) };
        _titleLabel.Location = new Point(0, 0);
        _subtitleLabel.Location = new Point(0, 42);
        header.Controls.Add(_titleLabel);
        header.Controls.Add(_subtitleLabel);
        return header;
    }

    private Control BuildToolbar()
    {
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = JiraTheme.BgPage, Padding = new Padding(20, 0, 20, 12) };
        var left = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = JiraTheme.BgPage, Margin = new Padding(0), Padding = new Padding(0) };
        left.Controls.Add(MakeFilterLabel("Sprint"));
        left.Controls.Add(_sprintFilter);
        left.Controls.Add(MakeFilterLabel("Assignee"));
        left.Controls.Add(_assigneeFilter);
        left.Controls.Add(_zoomLabel);
        var right = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.RightToLeft, BackColor = JiraTheme.BgPage, Margin = new Padding(0), Padding = new Padding(0) };
        right.Controls.Add(_refreshButton);
        toolbar.Controls.Add(right);
        toolbar.Controls.Add(left);
        return toolbar;
    }

    private Control BuildBody()
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 220, FixedPanel = FixedPanel.Panel1, BackColor = JiraTheme.BgPage, Panel1MinSize = 220, Panel2MinSize = 420 };
        split.Panel1.BackColor = JiraTheme.BgSurface;
        split.Panel2.BackColor = JiraTheme.BgPage;
        split.Panel1.Controls.Add(BuildSidebar());
        split.Panel2.Controls.Add(BuildTimelineAndDetail());
        return split;
    }

    private Control BuildSidebar()
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
        var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = JiraTheme.BgSurface, Padding = new Padding(16, 12, 16, 8) };
        var label = JiraControlFactory.CreateLabel("Epics", true);
        label.Dock = DockStyle.Fill;
        header.Controls.Add(label);
        host.Controls.Add(_epicList);
        host.Controls.Add(header);
        return host;
    }

    private Control BuildTimelineAndDetail()
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, FixedPanel = FixedPanel.Panel2, SplitterDistance = 430, Panel2MinSize = 210, BackColor = JiraTheme.BgPage };
        split.Panel1.BackColor = JiraTheme.BgSurface;
        split.Panel2.BackColor = JiraTheme.BgSurface;
        var timelineHost = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
        timelineHost.Controls.Add(_emptyState);
        timelineHost.Controls.Add(_timelineCanvas);
        split.Panel1.Controls.Add(timelineHost);
        split.Panel2.Controls.Add(_detailPanel);
        return split;
    }

    private Control BuildDetailHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 118, BackColor = JiraTheme.BgSurface };
        _detailTitle.Dock = DockStyle.Top;
        _detailMeta.Dock = DockStyle.Top;
        _detailProgress.Dock = DockStyle.Top;
        _openEpicButton.Dock = DockStyle.Right;
        _detailActionHost.Controls.Clear();
        _detailActionHost.Controls.Add(_openEpicButton);
        UpdateDetailActionButtonLayout();
        header.Controls.Add(_detailActionHost);
        header.Controls.Add(_detailProgressCaption);
        header.Controls.Add(_detailProgress);
        header.Controls.Add(_detailMeta);
        header.Controls.Add(_detailTitle);
        return header;
    }

    private Control BuildChildIssuesHost()
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface, Padding = new Padding(0, 10, 0, 0) };
        host.Controls.Add(_childIssuesEmpty);
        host.Controls.Add(_childIssuesList);
        return host;
    }

    private void OnRoadmapFormLoad(object? sender, EventArgs e) => _ = RefreshRoadmapAsync();
    private void OnRefreshButtonClick(object? sender, EventArgs e) => _ = RefreshRoadmapAsync();
    private void OnFilterChanged(object? sender, EventArgs e) => ApplyFilters(_selectedEpicId);
    private async void OnEpicListSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressSelectionChanged) return;
        if (_epicList.SelectedItem is not RoadmapEpicDto epic)
        {
            await SelectEpicAsync(null, false, false);
            return;
        }
        await SelectEpicAsync(epic.EpicId, false, true);
    }

    private void OnTimelineEpicClicked(object? sender, int epicId) => _ = SelectEpicAsync(epicId, true, false);
    private void OnTimelineEpicDoubleClicked(object? sender, int epicId) => _ = OpenEpicAsync(epicId);
    private void OnTimelineZoomChanged(object? sender, float e) => UpdateZoomLabel();
    private void OnOpenEpicButtonClick(object? sender, EventArgs e) { if (_selectedEpicId.HasValue) _ = OpenEpicAsync(_selectedEpicId.Value); }
    private void OnDetailActionHostResize(object? sender, EventArgs e) => UpdateDetailActionButtonLayout();

    private async void OnTimelineScheduleChanged(object? sender, RoadmapScheduleChangedEventArgs e)
    {
        try
        {
            var userId = _session.CurrentUserContext.RequireUserId();
            await _session.Issues.UpdateScheduleAsync(e.EpicId, e.StartDate, e.DueDate, userId);
            _allEpics = _allEpics.Select(epic => epic.EpicId == e.EpicId ? epic with { StartDate = e.StartDate, DueDate = e.DueDate } : epic).ToList();
            ApplyFilters(e.EpicId);
            await LoadSelectedEpicDetailsAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
            await RefreshRoadmapAsync();
        }
    }

    private void OnChildIssuesListDoubleClick(object? sender, EventArgs e)
    {
        if (_childIssuesList.SelectedItems.Count == 0 || _project is null) return;
        if (_childIssuesList.SelectedItems[0].Tag is not int issueId) return;
        using var dialog = new IssueDetailsForm(_session, issueId, _project.Id);
        if (dialog.ShowDialog(this) == DialogResult.OK) _ = RefreshRoadmapAsync();
    }

    private async Task SelectEpicAsync(int? epicId, bool syncList, bool scrollCanvas)
    {
        _selectedEpicId = epicId;
        _timelineCanvas.SelectEpic(epicId);
        if (scrollCanvas && epicId.HasValue) _timelineCanvas.ScrollToEpic(epicId.Value);
        if (syncList)
        {
            _suppressSelectionChanged = true;
            try
            {
                var selectedIndex = epicId.HasValue ? _filteredEpics.ToList().FindIndex(epic => epic.EpicId == epicId.Value) : -1;
                _epicList.SelectedIndex = selectedIndex;
            }
            finally
            {
                _suppressSelectionChanged = false;
            }
        }
        await LoadSelectedEpicDetailsAsync();
    }
    private async Task OpenEpicAsync(int epicId)
    {
        if (_project is null) return;
        using var dialog = new IssueDetailsForm(_session, epicId, _project.Id, openChildIssues: true);
        if (dialog.ShowDialog(this) == DialogResult.OK) await RefreshRoadmapAsync();
    }

    private async Task LoadSelectedEpicDetailsAsync()
    {
        if (!_selectedEpicId.HasValue)
        {
            BindDetail(null, Array.Empty<Issue>());
            return;
        }
        var epic = _filteredEpics.FirstOrDefault(item => item.EpicId == _selectedEpicId.Value) ?? _allEpics.FirstOrDefault(item => item.EpicId == _selectedEpicId.Value);
        if (epic is null)
        {
            BindDetail(null, Array.Empty<Issue>());
            return;
        }
        BindDetail(epic, Array.Empty<Issue>());
        var requestVersion = ++_detailRequestVersion;
        try
        {
            var childIssues = await _session.Issues.GetSubIssuesAsync(epic.EpicId);
            if (requestVersion != _detailRequestVersion) return;
            BindDetail(epic, childIssues);
        }
        catch (Exception exception)
        {
            if (requestVersion == _detailRequestVersion) ErrorDialogService.Show(exception);
        }
    }

    private void BindFilterChoices()
    {
        var selectedSprintId = _sprintFilter.SelectedValue as int? ?? 0;
        var selectedAssigneeId = _assigneeFilter.SelectedValue as int? ?? 0;
        var sprintOptions = new List<FilterOption> { new(0, "All sprints") };
        sprintOptions.AddRange(_sprints.Select(sprint => new FilterOption(sprint.Id, sprint.Name)));
        _sprintFilter.DataSource = sprintOptions;
        _sprintFilter.SelectedValue = sprintOptions.Any(option => option.Id == selectedSprintId) ? selectedSprintId : 0;
        var assigneeOptions = new List<FilterOption> { new(0, "All assignees") };
        assigneeOptions.AddRange(_users.Select(user => new FilterOption(user.Id, user.DisplayName)));
        _assigneeFilter.DataSource = assigneeOptions;
        _assigneeFilter.SelectedValue = assigneeOptions.Any(option => option.Id == selectedAssigneeId) ? selectedAssigneeId : 0;
    }

    private void ApplyFilters(int? preferredEpicId)
    {
        var sprintId = _sprintFilter.SelectedValue is int selectedSprintId ? selectedSprintId : 0;
        var assigneeId = _assigneeFilter.SelectedValue is int selectedAssigneeId ? selectedAssigneeId : 0;
        var search = _shellSearch.Trim();
        _filteredEpics = _allEpics
            .Where(epic => sprintId == 0 || epic.SprintIds.Contains(sprintId))
            .Where(epic => assigneeId == 0 || epic.AssigneeId == assigneeId)
            .Where(epic => string.IsNullOrWhiteSpace(search) || epic.Title.Contains(search, StringComparison.CurrentCultureIgnoreCase) || epic.IssueKey.Contains(search, StringComparison.CurrentCultureIgnoreCase) || (epic.AssigneeName?.Contains(search, StringComparison.CurrentCultureIgnoreCase) ?? false))
            .ToList();
        _suppressSelectionChanged = true;
        try
        {
            _epicList.BeginUpdate();
            _epicList.Items.Clear();
            foreach (var epic in _filteredEpics) _epicList.Items.Add(epic);
            var nextSelectedEpicId = preferredEpicId.HasValue && _filteredEpics.Any(epic => epic.EpicId == preferredEpicId.Value) ? preferredEpicId : _filteredEpics.FirstOrDefault()?.EpicId;
            var selectedIndex = nextSelectedEpicId.HasValue ? _filteredEpics.ToList().FindIndex(epic => epic.EpicId == nextSelectedEpicId.Value) : -1;
            _epicList.SelectedIndex = selectedIndex;
            _selectedEpicId = nextSelectedEpicId;
        }
        finally
        {
            _epicList.EndUpdate();
            _suppressSelectionChanged = false;
        }
        _timelineCanvas.SetEpics(_filteredEpics);
        _timelineCanvas.SelectEpic(_selectedEpicId);
        _emptyState.Visible = _filteredEpics.Count == 0;
        _timelineCanvas.Visible = _filteredEpics.Count > 0;
        _ = LoadSelectedEpicDetailsAsync();
    }

    private void BindDetail(RoadmapEpicDto? epic, IReadOnlyList<Issue> childIssues)
    {
        _detailTitle.Text = epic?.Title ?? "Select an epic";
        _detailMeta.Text = epic is null ? "Click an epic bar to inspect child issues and progress." : $"{epic.IssueKey} • {epic.Status} • {BuildDateRangeText(epic)}{(string.IsNullOrWhiteSpace(epic.AssigneeName) ? string.Empty : $" • {epic.AssigneeName}")}";
        if (epic is null)
        {
            _detailProgress.Maximum = 1;
            _detailProgress.Value = 0;
            _detailProgressCaption.Text = string.Empty;
            _openEpicButton.Enabled = false;
            _childIssuesList.Items.Clear();
            _childIssuesList.Visible = false;
            _childIssuesEmpty.Visible = true;
            return;
        }
        _openEpicButton.Enabled = true;
        var completed = epic.TotalStoryPoints > 0 ? epic.DoneStoryPoints : epic.DoneCount;
        var total = epic.TotalStoryPoints > 0 ? epic.TotalStoryPoints : epic.ChildIssueCount;
        _detailProgress.Maximum = Math.Max(1, total);
        _detailProgress.Value = Math.Clamp(completed, 0, _detailProgress.Maximum);
        _detailProgressCaption.Text = epic.TotalStoryPoints > 0 ? $"Progress: {epic.DoneStoryPoints}/{epic.TotalStoryPoints} story points completed" : $"Progress: {epic.DoneCount}/{epic.ChildIssueCount} child issues done";
        _childIssuesList.BeginUpdate();
        try
        {
            _childIssuesList.Items.Clear();
            foreach (var issue in childIssues.OrderBy(issue => issue.WorkflowStatus.DisplayOrder).ThenBy(issue => issue.Title))
            {
                var item = new ListViewItem(issue.IssueKey) { Tag = issue.Id, BackColor = issue.WorkflowStatus.Category == StatusCategory.Done ? JiraTheme.DoneBadgeBg : JiraTheme.BgSurface };
                item.SubItems.Add(issue.Title);
                item.SubItems.Add(issue.WorkflowStatus.Name);
                item.SubItems.Add(issue.StoryPoints?.ToString() ?? "-");
                _childIssuesList.Items.Add(item);
            }
        }
        finally
        {
            _childIssuesList.EndUpdate();
        }
        _childIssuesEmpty.Visible = childIssues.Count == 0;
        _childIssuesList.Visible = childIssues.Count > 0;
    }

    private void OnEpicListDrawItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= _epicList.Items.Count) return;
        var epic = (RoadmapEpicDto)_epicList.Items[e.Index]!;
        var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        using var backgroundBrush = new SolidBrush(isSelected ? JiraTheme.Blue100 : JiraTheme.BgSurface);
        e.Graphics.FillRectangle(backgroundBrush, e.Bounds);
        using var dotBrush = new SolidBrush(ParseColor(epic.Color, ResolveCategoryColor(epic.StatusCategory)));
        e.Graphics.FillEllipse(dotBrush, new Rectangle(e.Bounds.X + 14, e.Bounds.Y + 22, 8, 8));
        DrawAvatar(e.Graphics, new Rectangle(e.Bounds.X + 30, e.Bounds.Y + 14, 28, 28), epic.AssigneeName);
        TextRenderer.DrawText(e.Graphics, epic.Title, JiraTheme.FontSmall, new Rectangle(e.Bounds.X + 70, e.Bounds.Y + 10, e.Bounds.Width - 86, 18), JiraTheme.TextPrimary, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(e.Graphics, $"{epic.DoneCount}/{epic.ChildIssueCount} done • {BuildDateRangeText(epic)}", JiraTheme.FontCaption, new Rectangle(e.Bounds.X + 70, e.Bounds.Y + 28, e.Bounds.Width - 86, 18), JiraTheme.TextSecondary, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
        if (e.Index < _epicList.Items.Count - 1)
        {
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawLine(pen, e.Bounds.X + 14, e.Bounds.Bottom - 1, e.Bounds.Right - 14, e.Bounds.Bottom - 1);
        }
        e.DrawFocusRectangle();
    }

    private void UpdateZoomLabel() => _zoomLabel.Text = $"Zoom: {_timelineCanvas.GetScaleLabel()}";
    private void UpdateDetailActionButtonLayout() => _openEpicButton.Location = new Point(Math.Max(0, _detailActionHost.Width - _openEpicButton.Width), 0);
    private static ComboBox CreateFilterCombo(int width) => new() { Width = width, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody, Margin = new Padding(6, 0, 16, 0) };
    private static Label MakeFilterLabel(string text) { var label = JiraControlFactory.CreateLabel(text, true); label.Margin = new Padding(0, 10, 6, 0); return label; }
    private static string BuildDateRangeText(RoadmapEpicDto epic) => epic.StartDate.HasValue && epic.DueDate.HasValue ? $"{epic.StartDate:dd MMM} - {epic.DueDate:dd MMM}" : epic.StartDate.HasValue ? $"Starts {epic.StartDate:dd MMM}" : epic.DueDate.HasValue ? $"Due {epic.DueDate:dd MMM}" : "Unscheduled";
    private static Color ResolveCategoryColor(StatusCategory category) => category switch { StatusCategory.Done => JiraTheme.Green700, StatusCategory.InProgress => JiraTheme.Blue600, _ => JiraTheme.Neutral700 };
    private static Color ParseColor(string? value, Color fallback) { try { return string.IsNullOrWhiteSpace(value) ? fallback : ColorTranslator.FromHtml(value); } catch { return fallback; } }
    private static void DrawAvatar(Graphics graphics, Rectangle bounds, string? displayName) { using var brush = new SolidBrush(ResolveAvatarColor(displayName)); graphics.SmoothingMode = SmoothingMode.AntiAlias; graphics.FillEllipse(brush, bounds); TextRenderer.DrawText(graphics, BuildInitials(displayName), JiraTheme.FontCaption, bounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); }
    private static string BuildInitials(string? value) { if (string.IsNullOrWhiteSpace(value)) return "?"; var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); return parts.Length == 1 ? parts[0][..1].ToUpperInvariant() : string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0]))); }
    private static Color ResolveAvatarColor(string? value) { var palette = new[] { JiraTheme.Blue600, JiraTheme.Teal500, JiraTheme.Green700, JiraTheme.Orange400, JiraTheme.Purple500 }; return string.IsNullOrWhiteSpace(value) ? JiraTheme.Neutral500 : palette[Math.Abs(value.GetHashCode()) % palette.Length]; }

    private sealed record FilterOption(int Id, string Label);

    private sealed class RoadmapTimelineCanvas : ScrollableControl
    {
        private const int HeaderHeight = 58;
        private const int RowHeight = 48;
        private const int RowPadding = 8;
        private const int BarHeight = 30;
        private const int LeftPadding = 24;
        private const int RightPadding = 40;
        private const float MinPixelsPerDay = 2f;
        private const float MaxPixelsPerDay = 20f;

        private IReadOnlyList<RoadmapEpicDto> _epics = Array.Empty<RoadmapEpicDto>();
        private readonly List<BarLayout> _barLayouts = [];
        private float _pixelsPerDay = 6f;
        private DateOnly _rangeStart = DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
        private DateOnly _rangeEnd = DateOnly.FromDateTime(DateTime.Today.AddDays(21));
        private int? _selectedEpicId;
        private DragContext? _drag;

        public RoadmapTimelineCanvas()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            AutoScroll = true;
            BackColor = JiraTheme.BgSurface;
        }

        public event EventHandler<int>? EpicClicked;
        public event EventHandler<int>? EpicDoubleClicked;
        public event EventHandler<RoadmapScheduleChangedEventArgs>? ScheduleChanged;
        public event EventHandler<float>? ZoomChanged;

        public void SetEpics(IReadOnlyList<RoadmapEpicDto> epics)
        {
            _epics = epics.ToList();
            if (_selectedEpicId.HasValue && !_epics.Any(epic => epic.EpicId == _selectedEpicId.Value)) _selectedEpicId = null;
            RecalculateRange();
            Invalidate();
        }

        public void SelectEpic(int? epicId) { _selectedEpicId = epicId; Invalidate(); }
        public void ScrollToEpic(int epicId) { var index = _epics.ToList().FindIndex(epic => epic.EpicId == epicId); if (index >= 0) AutoScrollPosition = new Point(Math.Abs(AutoScrollPosition.X), index * RowHeight); }
        public string GetScaleLabel() => ResolveScale() switch { TimelineScale.Day => "Day view", TimelineScale.Week => "Week view", _ => "Month view" };

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.Graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
            _barLayouts.Clear();
            DrawBackground(e.Graphics);
            DrawTimeHeader(e.Graphics);
            DrawTodayLine(e.Graphics);
            DrawEpicBars(e.Graphics);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            var previous = _pixelsPerDay;
            _pixelsPerDay = Math.Clamp(_pixelsPerDay + (e.Delta > 0 ? 1f : -1f), MinPixelsPerDay, MaxPixelsPerDay);
            if (Math.Abs(previous - _pixelsPerDay) > float.Epsilon)
            {
                RecalculateRange();
                Invalidate();
                ZoomChanged?.Invoke(this, _pixelsPerDay);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            var hit = HitTest(TranslateToCanvas(e.Location));
            if (hit is null) return;
            _selectedEpicId = hit.Epic.EpicId;
            EpicClicked?.Invoke(this, hit.Epic.EpicId);
            _drag = new DragContext(hit.Epic.EpicId, hit.IsResizeHandle ? DragMode.ResizeEnd : DragMode.Move, hit.DisplayStart, hit.DisplayEnd, hit.DisplayStart, hit.DisplayEnd, TranslateToCanvas(e.Location));
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var canvasPoint = TranslateToCanvas(e.Location);
            if (_drag is null)
            {
                var hover = HitTest(canvasPoint);
                Cursor = hover is null ? Cursors.Default : hover.IsResizeHandle ? Cursors.SizeWE : Cursors.SizeAll;
                return;
            }
            var deltaDays = (int)Math.Round((canvasPoint.X - _drag.OriginPoint.X) / _pixelsPerDay);
            var nextStart = _drag.OriginalStart;
            var nextEnd = _drag.OriginalEnd;
            if (_drag.Mode == DragMode.Move)
            {
                nextStart = _drag.OriginalStart.AddDays(deltaDays);
                nextEnd = _drag.OriginalEnd.AddDays(deltaDays);
            }
            else
            {
                nextEnd = _drag.OriginalEnd.AddDays(deltaDays);
                if (nextEnd < _drag.OriginalStart) nextEnd = _drag.OriginalStart;
            }
            _drag = _drag with { PreviewStart = nextStart, PreviewEnd = nextEnd };
            Invalidate();
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_drag is null) return;
            Capture = false;
            var drag = _drag;
            _drag = null;
            Invalidate();
            if (drag.HasChanged) ScheduleChanged?.Invoke(this, new RoadmapScheduleChangedEventArgs(drag.EpicId, drag.PreviewStart, drag.PreviewEnd));
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left) return;
            var hit = HitTest(TranslateToCanvas(e.Location));
            if (hit is not null) EpicDoubleClicked?.Invoke(this, hit.Epic.EpicId);
        }

        private void DrawBackground(Graphics graphics)
        {
            using var backgroundBrush = new SolidBrush(JiraTheme.BgSurface);
            graphics.FillRectangle(backgroundBrush, new Rectangle(0, 0, AutoScrollMinSize.Width, Math.Max(Height, AutoScrollMinSize.Height)));
            using var headerBrush = new SolidBrush(JiraTheme.Neutral100);
            graphics.FillRectangle(headerBrush, 0, 0, AutoScrollMinSize.Width, HeaderHeight);
            using var headerBorder = new Pen(JiraTheme.Border);
            graphics.DrawLine(headerBorder, 0, HeaderHeight - 1, AutoScrollMinSize.Width, HeaderHeight - 1);
        }

        private void DrawTimeHeader(Graphics graphics)
        {
            var scale = ResolveScale();
            var monthStart = new DateOnly(_rangeStart.Year, _rangeStart.Month, 1);
            while (monthStart <= _rangeEnd)
            {
                var nextMonth = monthStart.AddMonths(1);
                var segmentEnd = nextMonth.AddDays(-1) < _rangeEnd ? nextMonth.AddDays(-1) : _rangeEnd;
                var bounds = Rectangle.Round(new RectangleF(DateToX(monthStart), 0, Math.Max(30f, DateToX(segmentEnd.AddDays(1)) - DateToX(monthStart)), 28));
                TextRenderer.DrawText(graphics, monthStart.ToString("MMM yyyy", CultureInfo.InvariantCulture), JiraTheme.FontCaption, bounds, JiraTheme.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                monthStart = nextMonth;
            }
            switch (scale)
            {
                case TimelineScale.Day:
                    for (var day = _rangeStart; day <= _rangeEnd; day = day.AddDays(1))
                    {
                        var x = DateToX(day);
                        TextRenderer.DrawText(graphics, day.ToString("dd", CultureInfo.InvariantCulture), JiraTheme.FontCaption, Rectangle.Round(new RectangleF(x, 28, _pixelsPerDay, 24)), JiraTheme.TextPrimary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                        using var pen = new Pen(Color.FromArgb(24, JiraTheme.Border));
                        graphics.DrawLine(pen, x, HeaderHeight, x, AutoScrollMinSize.Height);
                    }
                    break;
                case TimelineScale.Week:
                    var weekStart = AlignToWeekStart(_rangeStart);
                    while (weekStart <= _rangeEnd)
                    {
                        var x = DateToX(weekStart);
                        var nextWeek = weekStart.AddDays(7);
                        TextRenderer.DrawText(graphics, weekStart.ToString("dd MMM", CultureInfo.InvariantCulture), JiraTheme.FontCaption, Rectangle.Round(new RectangleF(x, 28, Math.Max(50f, DateToX(nextWeek) - x), 24)), JiraTheme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                        using var pen = new Pen(Color.FromArgb(40, JiraTheme.Border));
                        graphics.DrawLine(pen, x, 28, x, AutoScrollMinSize.Height);
                        weekStart = nextWeek;
                    }
                    break;
                default:
                    var current = AlignToWeekStart(_rangeStart);
                    while (current <= _rangeEnd)
                    {
                        var x = DateToX(current);
                        var next = current.AddDays(7);
                        TextRenderer.DrawText(graphics, $"W{ISOWeek.GetWeekOfYear(current.ToDateTime(TimeOnly.MinValue))}", JiraTheme.FontCaption, Rectangle.Round(new RectangleF(x, 28, Math.Max(44f, DateToX(next) - x), 24)), JiraTheme.TextPrimary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                        using var pen = new Pen(Color.FromArgb(28, JiraTheme.Border));
                        graphics.DrawLine(pen, x, 28, x, AutoScrollMinSize.Height);
                        current = next;
                    }
                    break;
            }
        }

        private void DrawTodayLine(Graphics graphics)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (today < _rangeStart || today > _rangeEnd) return;
            var x = DateToX(today) + (_pixelsPerDay / 2f);
            using var pen = new Pen(JiraTheme.Red600, 2f);
            graphics.DrawLine(pen, x, 0, x, AutoScrollMinSize.Height);
        }

        private void DrawEpicBars(Graphics graphics)
        {
            for (var index = 0; index < _epics.Count; index++)
            {
                var epic = _epics[index];
                var rowBounds = new Rectangle(0, HeaderHeight + (index * RowHeight), AutoScrollMinSize.Width, RowHeight);
                using var rowBrush = new SolidBrush(index % 2 == 0 ? JiraTheme.BgSurface : JiraTheme.AlternateRowBg);
                graphics.FillRectangle(rowBrush, rowBounds);
                using var separatorPen = new Pen(Color.FromArgb(36, JiraTheme.Border));
                graphics.DrawLine(separatorPen, 0, rowBounds.Bottom - 1, AutoScrollMinSize.Width, rowBounds.Bottom - 1);
                var displayRange = ResolveDisplayRange(epic);
                if (_drag is not null && _drag.EpicId == epic.EpicId) displayRange = (_drag.PreviewStart, _drag.PreviewEnd);
                var barX = DateToX(displayRange.Start);
                var barWidth = Math.Max(_pixelsPerDay + 10f, DateToX(displayRange.End.AddDays(1)) - barX - 6f);
                var barBounds = new RectangleF(barX, rowBounds.Y + RowPadding, barWidth, BarHeight);
                _barLayouts.Add(new BarLayout(epic, barBounds, displayRange.Start, displayRange.End));
                var fillColor = ParseColor(epic.Color, ResolveCategoryColor(epic.StatusCategory));
                using var path = GraphicsHelper.CreateRoundedPath(Rectangle.Round(barBounds), 8);
                using var fillBrush = new SolidBrush(fillColor);
                graphics.FillPath(fillBrush, path);
                using var pen = new Pen(epic.EpicId == _selectedEpicId ? JiraTheme.Blue700 : Color.FromArgb(90, Color.Black), epic.EpicId == _selectedEpicId ? 2f : 1f);
                graphics.DrawPath(pen, path);
                TextRenderer.DrawText(graphics, epic.Title, JiraTheme.FontSmall, Rectangle.Round(new RectangleF(barBounds.X + 12, barBounds.Y, Math.Max(40, barBounds.Width - 60), barBounds.Height)), Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                if (barBounds.Width > 110)
                {
                    var progressBounds = new Rectangle((int)(barBounds.Right - 46), (int)barBounds.Y + 6, 34, 18);
                    using var progressPath = GraphicsHelper.CreateRoundedPath(progressBounds, progressBounds.Height / 2);
                    using var progressBrush = new SolidBrush(Color.FromArgb(48, Color.White));
                    graphics.FillPath(progressBrush, progressPath);
                    TextRenderer.DrawText(graphics, $"{epic.DoneCount}/{Math.Max(epic.ChildIssueCount, 0)}", JiraTheme.FontCaption, progressBounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }

        private void RecalculateRange()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (_epics.Count == 0)
            {
                _rangeStart = today.AddDays(-7);
                _rangeEnd = today.AddDays(21);
            }
            else
            {
                var ranges = _epics.Select(ResolveDisplayRange).ToList();
                _rangeStart = ranges.Min(range => range.Start).AddDays(-3);
                _rangeEnd = ranges.Max(range => range.End).AddDays(7);
                if (_rangeEnd < _rangeStart) (_rangeStart, _rangeEnd) = (_rangeEnd, _rangeStart);
            }
            var totalDays = Math.Max(1, _rangeEnd.DayNumber - _rangeStart.DayNumber + 1);
            AutoScrollMinSize = new Size((int)Math.Ceiling(LeftPadding + RightPadding + (totalDays * _pixelsPerDay)), HeaderHeight + Math.Max(1, _epics.Count) * RowHeight + 24);
        }

        private float DateToX(DateOnly date) => LeftPadding + ((date.DayNumber - _rangeStart.DayNumber) * _pixelsPerDay);
        private PointF TranslateToCanvas(Point point) => new(point.X - AutoScrollPosition.X, point.Y - AutoScrollPosition.Y);
        private BarLayout? HitTest(PointF canvasPoint)
        {
            foreach (var layout in _barLayouts.AsEnumerable().Reverse())
            {
                if (layout.Bounds.Contains(canvasPoint)) return layout with { IsResizeHandle = canvasPoint.X >= layout.Bounds.Right - 10 };
            }
            return null;
        }
        private TimelineScale ResolveScale() => _pixelsPerDay switch { >= 11f => TimelineScale.Day, >= 5f => TimelineScale.Week, _ => TimelineScale.Month };
        private static DateOnly AlignToWeekStart(DateOnly value) { var current = value; while (current.DayOfWeek != DayOfWeek.Monday) current = current.AddDays(-1); return current; }
        private static (DateOnly Start, DateOnly End) ResolveDisplayRange(RoadmapEpicDto epic)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (epic.StartDate.HasValue && epic.DueDate.HasValue) return epic.StartDate.Value <= epic.DueDate.Value ? (epic.StartDate.Value, epic.DueDate.Value) : (epic.DueDate.Value, epic.StartDate.Value);
            if (epic.StartDate.HasValue) return (epic.StartDate.Value, epic.StartDate.Value.AddDays(7));
            if (epic.DueDate.HasValue) return (epic.DueDate.Value.AddDays(-7), epic.DueDate.Value);
            return (today.AddDays(-3), today.AddDays(3));
        }

        private sealed record BarLayout(RoadmapEpicDto Epic, RectangleF Bounds, DateOnly DisplayStart, DateOnly DisplayEnd, bool IsResizeHandle = false);
        private sealed record DragContext(int EpicId, DragMode Mode, DateOnly OriginalStart, DateOnly OriginalEnd, DateOnly PreviewStart, DateOnly PreviewEnd, PointF OriginPoint) { public bool HasChanged => PreviewStart != OriginalStart || PreviewEnd != OriginalEnd; }
        private enum DragMode { Move, ResizeEnd }
        private enum TimelineScale { Month, Week, Day }
    }

    private sealed record RoadmapScheduleChangedEventArgs(int EpicId, DateOnly? StartDate, DateOnly? DueDate);
}
