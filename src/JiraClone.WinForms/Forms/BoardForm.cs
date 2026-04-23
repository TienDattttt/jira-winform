using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Controls;
using JiraClone.WinForms.Helpers;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public class BoardForm : UserControl
{
    private readonly AppSession _session;
    private readonly bool _activeSprintOnly;
    private readonly Label _sprintTitleLabel = JiraControlFactory.CreateLabel("Chưa có sprint đang hoạt động");
    private readonly Label _sprintDateLabel = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly Button _startSprintButton = JiraControlFactory.CreatePrimaryButton("Bắt đầu sprint");
    private readonly Button _boardModeButton = JiraControlFactory.CreateSecondaryButton("Chế độ: Scrum");
    private readonly ComboBox _assigneeFilter = CreateFilterCombo(210);
    private readonly ComboBox _priorityFilter = CreateFilterCombo(170);
    private readonly ComboBox _typeFilter = CreateFilterCombo(160);
    private readonly TextBox _searchFilter = JiraControlFactory.CreateTextBox();
    private readonly Button _groupByEpicButton = JiraControlFactory.CreateSecondaryButton("Nhóm theo Epic");
    private readonly Button _clearFiltersButton = JiraControlFactory.CreateSecondaryButton("Xóa bộ lọc");
    private readonly Dictionary<int, BoardColumnControl> _columnControls = new();
    private readonly HashSet<string> _collapsedLaneKeys = [];
    private readonly Panel _toastPanel = new();
    private readonly Label _toastLabel = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly System.Windows.Forms.Timer _toastTimer = new() { Interval = 2600 };
    private readonly Panel _topBar = new();
    private readonly FlowLayoutPanel _filterBar = new();
    private readonly FlowLayoutPanel _boardColumnsPanel = new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = false,
        BackColor = JiraTheme.BgPage,
        Margin = new Padding(0),
        Padding = new Padding(0),
    };
    private readonly FlowLayoutPanel _swimlanesPanel = new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        BackColor = JiraTheme.BgPage,
        Margin = new Padding(0),
        Padding = new Padding(0),
        Visible = false,
    };
    private readonly Panel _boardContentHost = new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        BackColor = JiraTheme.BgPage,
        Margin = new Padding(0),
        Padding = new Padding(0),
    };
    private readonly Panel _boardScrollPanel = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        BackColor = JiraTheme.BgPage,
        Padding = new Padding(16, 12, 16, 16),
    };

    private Project? _project;
    private int _projectId;
    private Sprint? _activeSprint;
    private IReadOnlyList<BoardColumnDto> _loadedColumns = Array.Empty<BoardColumnDto>();
    private bool _groupByEpic;
    private BoardType _boardType = BoardType.Scrum;
    private TimeSpan? _averageCycleTime;
    private bool _suppressFilterEvents;
    private bool _canChangeBoardMode;
    private Color _toastAccentColor = JiraTheme.Blue600;
    private readonly CancellationTokenSource _disposeCts = new();
    private CancellationTokenSource? _loadCts;

    public BoardForm(AppSession session, bool activeSprintOnly = true)
    {
        _session = session;
        _activeSprintOnly = activeSprintOnly;
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;

        _sprintTitleLabel.Font = JiraTheme.FontH2;
        _sprintTitleLabel.AutoSize = true;
        _sprintDateLabel.Font = JiraTheme.FontCaption;
        _sprintDateLabel.AutoSize = true;

        _startSprintButton.AutoSize = false;
        _startSprintButton.Size = new Size(168, 40);
        _startSprintButton.Click += OnStartSprintButtonClick;

        _boardModeButton.AutoSize = false;
        _boardModeButton.Size = new Size(172, 40);
        _boardModeButton.Click += OnBoardModeButtonClick;

        _searchFilter.Width = 280;
        _searchFilter.PlaceholderText = "Tìm kiếm công việc";
        _groupByEpicButton.AutoSize = false;
        _groupByEpicButton.Size = new Size(148, 40);
        _clearFiltersButton.AutoSize = false;
        _clearFiltersButton.Size = new Size(132, 40);
        _assigneeFilter.Margin = new Padding(0, 0, 12, 0);
        _priorityFilter.Margin = new Padding(0, 0, 12, 0);
        _typeFilter.Margin = new Padding(0, 0, 12, 0);
        _searchFilter.Margin = new Padding(0, 0, 12, 0);
        _groupByEpicButton.Margin = new Padding(0, 0, 12, 0);
        _clearFiltersButton.Margin = new Padding(0, 0, 12, 0);
        _boardModeButton.Margin = new Padding(0);

        _assigneeFilter.SelectedIndexChanged += OnFilterChanged;
        _priorityFilter.SelectedIndexChanged += OnFilterChanged;
        _typeFilter.SelectedIndexChanged += OnFilterChanged;
        _searchFilter.TextChanged += OnFilterChanged;
        _groupByEpicButton.Click += OnGroupByEpicButtonClick;
        _clearFiltersButton.Click += OnClearFiltersButtonClick;

        ConfigureToast();
        UpdateGroupByEpicButton();
        UpdateBoardModeButton();

        _boardContentHost.Controls.Add(_swimlanesPanel);
        _boardContentHost.Controls.Add(_boardColumnsPanel);
        _boardScrollPanel.Controls.Add(_boardContentHost);

        Controls.Add(_toastPanel);
        Controls.Add(_boardScrollPanel);
        Controls.Add(BuildFilterBar());
        Controls.Add(BuildTopBar());

        Load += OnBoardLoad;
        Resize += OnBoardResize;
    }

    public Task RefreshBoardAsync(CancellationToken cancellationToken = default) => ReloadBoardAsync(cancellationToken);

    private Task ReloadBoardAsync(CancellationToken cancellationToken = default) => LoadBoardAsync(RestartLoadCancellation(cancellationToken));

    private CancellationToken RestartLoadCancellation(CancellationToken cancellationToken = default)
    {
        CancelPendingLoad();
        _loadCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, cancellationToken);
        return _loadCts.Token;
    }

    private CancellationTokenSource CreateOperationSource(CancellationToken cancellationToken = default) =>
        CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, cancellationToken);

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

    public void SetShellSearch(string searchText)
    {
        var value = searchText ?? string.Empty;
        if (string.Equals(_searchFilter.Text, value, StringComparison.Ordinal))
        {
            return;
        }

        _suppressFilterEvents = true;
        try
        {
            _searchFilter.Text = value;
        }
        finally
        {
            _suppressFilterEvents = false;
        }

        ApplyFilters();
    }

    private void ConfigureToast()
    {
        _toastPanel.Visible = false;
        _toastPanel.Size = new Size(360, 56);
        _toastPanel.BackColor = JiraTheme.BgSurface;
        _toastPanel.Padding = new Padding(14, 10, 14, 10);

        _toastLabel.Dock = DockStyle.Fill;
        _toastLabel.Font = JiraTheme.FontSmall;
        _toastLabel.ForeColor = JiraTheme.TextPrimary;

        _toastPanel.Controls.Add(_toastLabel);
        _toastPanel.Paint += OnToastPanelPaint;
        _toastTimer.Tick += OnToastTimerTick;
    }

    private Control BuildTopBar()
    {
        _topBar.Dock = DockStyle.Top;
        _topBar.Height = 80;
        _topBar.BackColor = JiraTheme.BgSurface;
        _topBar.Padding = new Padding(16, 10, 16, 10);
        _topBar.Paint += OnTopBarPaint;

        var right = new Panel
        {
            Dock = DockStyle.Right,
            Width = 220,
            BackColor = JiraTheme.BgSurface,
        };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = JiraTheme.BgSurface,
            Margin = new Padding(0),
            Padding = new Padding(0, 11, 0, 0),
        };
        actions.Controls.Add(_startSprintButton);
        right.Controls.Add(actions);

        var left = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
        };
        left.Controls.Add(_sprintTitleLabel);
        left.Controls.Add(_sprintDateLabel);
        _sprintTitleLabel.Location = new Point(0, 0);
        _sprintDateLabel.Location = new Point(0, 34);

        _topBar.Controls.Clear();
        _topBar.Controls.Add(right);
        _topBar.Controls.Add(left);
        return _topBar;
    }

    private Control BuildFilterBar()
    {
        var host = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = JiraTheme.BgPage,
            Padding = new Padding(16, 10, 16, 10),
        };

        _filterBar.Dock = DockStyle.Fill;
        _filterBar.BackColor = JiraTheme.BgSurface;
        _filterBar.Padding = new Padding(12, 6, 12, 6);
        _filterBar.WrapContents = false;
        _filterBar.Margin = new Padding(0);
        _filterBar.Paint += OnFilterBarPaint;

        _filterBar.Controls.Clear();
        _filterBar.Controls.Add(_assigneeFilter);
        _filterBar.Controls.Add(_priorityFilter);
        _filterBar.Controls.Add(_typeFilter);
        _filterBar.Controls.Add(_searchFilter);
        _filterBar.Controls.Add(_groupByEpicButton);
        _filterBar.Controls.Add(_clearFiltersButton);
        _filterBar.Controls.Add(_boardModeButton);
        host.Controls.Add(_filterBar);
        return host;
    }

    private static ComboBox CreateFilterCombo(int width)
    {
        var comboBox = new ComboBox
        {
            Width = width,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = JiraTheme.BgSurface,
            ForeColor = JiraTheme.TextPrimary,
            Font = JiraTheme.FontBody,
            IntegralHeight = false
        };
        LayoutHelper.ConfigureComboBox(comboBox);
        return comboBox;
    }

    private async Task LoadBoardAsync(CancellationToken cancellationToken = default)
    {
        if (!Visible)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Project? project = null;
            Sprint? activeSprint = null;
            IReadOnlyList<BoardColumnDto> columns = Array.Empty<BoardColumnDto>();
            TimeSpan? averageCycleTime = null;
            var currentUserId = _session.CurrentUserContext.RequireUserId();

            await _session.RunSerializedAsync(async () =>
            {
                project = await _session.Projects.GetActiveProjectAsync(cancellationToken);
                if (project is null)
                {
                    return;
                }

                _canChangeBoardMode = await _session.Permissions.HasPermissionAsync(currentUserId, project.Id, Permission.ManageProject, cancellationToken);

                if (project.BoardType == BoardType.Scrum)
                {
                    activeSprint = await _session.Sprints.GetActiveByProjectAsync(project.Id, cancellationToken);
                    columns = _activeSprintOnly
                        ? await _session.Board.GetBoardAsync(project.Id, activeSprint?.Id, cancellationToken)
                        : await _session.Board.GetBoardAsync(project.Id, cancellationToken);
                }
                else
                {
                    columns = await _session.Board.GetBoardAsync(project.Id, cancellationToken);
                    averageCycleTime = await _session.Board.GetAverageCycleTimeAsync(project.Id, cancellationToken);
                }
            }, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            if (project is null)
            {
                ErrorDialogService.Show("Không tìm thấy dự án đang hoạt động.");
                return;
            }

            _project = project;
            _projectId = project.Id;
            _boardType = project.BoardType;
            _activeSprint = activeSprint;
            _loadedColumns = columns;
            _averageCycleTime = averageCycleTime;
            UpdateBoardModeButton();
            PopulateHeader();
            PopulateFilterOptions(_loadedColumns);
            ApplyFilters();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
        finally
        {
        }
    }

    private void PopulateHeader()
    {
        if (_boardType == BoardType.Kanban)
        {
            _sprintTitleLabel.Text = $"{_project?.Name ?? "Dự án"} - Kanban";
            _sprintDateLabel.Text = BuildKanbanSubtitle();
            _startSprintButton.Enabled = false;
            _startSprintButton.Visible = false;
            _startSprintButton.Text = "Bắt đầu sprint";
            return;
        }

        if (_activeSprint is null)
        {
            _sprintTitleLabel.Text = _activeSprintOnly ? "Chưa có sprint đang hoạt động" : IssueDisplayText.TranslateStatus("Backlog");
            _sprintDateLabel.Text = _activeSprintOnly ? "Hãy bắt đầu một sprint đã lập kế hoạch để tập trung bảng công việc" : "Đang hiển thị tất cả công việc của dự án";
            _startSprintButton.Enabled = _activeSprintOnly;
            _startSprintButton.Visible = _activeSprintOnly;
            _startSprintButton.Text = "Bắt đầu sprint";
            return;
        }

        _sprintTitleLabel.Text = _activeSprintOnly ? _activeSprint.Name : IssueDisplayText.TranslateStatus("Backlog");
        _sprintDateLabel.Text = _activeSprintOnly ? FormatSprintDateRange(_activeSprint) : $"Sprint đang hoạt động: {_activeSprint.Name}";
        _startSprintButton.Enabled = false;
        _startSprintButton.Visible = _activeSprintOnly;
        _startSprintButton.Text = "Đang chạy";
    }

    private string BuildKanbanSubtitle()
    {
        var parts = new List<string> { $"Hiển thị tất cả issue ngoài {IssueDisplayText.TranslateStatus("Done")}" };
        parts.Add(_averageCycleTime.HasValue
            ? $"Thời gian chu kỳ trung bình: {FormatCycleTime(_averageCycleTime.Value)}"
            : "Thời gian chu kỳ trung bình: không có");
        return string.Join("  -  ", parts);
    }

    private static string FormatCycleTime(TimeSpan duration)
    {
        if (duration.TotalDays >= 1d)
        {
            return $"{duration.TotalDays:0.0} ngày";
        }

        if (duration.TotalHours >= 1d)
        {
            return $"{duration.TotalHours:0.#} giờ";
        }

        return $"{Math.Max(1d, duration.TotalMinutes):0} phút";
    }

    private static string FormatSprintDateRange(Sprint sprint)
    {
        if (!sprint.StartDate.HasValue && !sprint.EndDate.HasValue)
        {
            return "Không có khoảng thời gian";
        }

        var start = sprint.StartDate?.ToString("dd MMM yyyy") ?? "?";
        var end = sprint.EndDate?.ToString("dd MMM yyyy") ?? "?";
        return $"{start} - {end}";
    }

    private void PopulateFilterOptions(IReadOnlyList<BoardColumnDto> columns)
    {
        var issues = columns.SelectMany(x => x.Issues).ToList();
        var assignees = issues.SelectMany(x => x.AssigneeNames).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

        _suppressFilterEvents = true;
        try
        {
            ResetCombo(_assigneeFilter, "Tất cả người được giao", assignees);
            ResetCombo(_priorityFilter, "Tất cả độ ưu tiên", Enum.GetNames<IssuePriority>());
            ResetCombo(_typeFilter, "Tất cả loại", Enum.GetNames<IssueType>());
        }
        finally
        {
            _suppressFilterEvents = false;
        }
    }

    private static void ResetCombo(ComboBox comboBox, string allLabel, IEnumerable<string> values)
    {
        var selected = comboBox.SelectedItem as string;
        comboBox.Items.Clear();
        comboBox.Items.Add(allLabel);
        foreach (var value in values)
        {
            comboBox.Items.Add(value);
        }

        var target = selected is not null && comboBox.Items.Contains(selected)
            ? selected
            : allLabel;
        comboBox.SelectedItem = target;
    }

    private void ApplyFilters()
    {
        var filteredColumns = _loadedColumns.Select(GetFilteredColumn).ToList();
        if (_groupByEpic)
        {
            RenderSwimlanes(BuildSwimlanes(filteredColumns));
        }
        else
        {
            RenderColumns(filteredColumns);
        }
    }

    private BoardColumnDto GetFilteredColumn(BoardColumnDto column)
    {
        var assignee = _assigneeFilter.SelectedIndex <= 0 ? null : _assigneeFilter.SelectedItem as string;
        var priority = _priorityFilter.SelectedIndex <= 0 ? null : _priorityFilter.SelectedItem as string;
        var type = _typeFilter.SelectedIndex <= 0 ? null : _typeFilter.SelectedItem as string;
        var search = _searchFilter.Text.Trim();
        var boardIssues = GetBoardModeIssues(column);

        var issues = boardIssues
            .Where(issue => IssueMatchesFilters(issue, assignee, priority, type, search))
            .OrderBy(issue => issue.BoardPosition)
            .ToList();

        return column with
        {
            Issues = issues,
            TotalIssueCount = boardIssues.Count
        };
    }

    private IReadOnlyList<IssueSummaryDto> GetBoardModeIssues(BoardColumnDto column)
    {
        if (_boardType == BoardType.Kanban)
        {
            return column.Issues
                .Where(issue => issue.StatusCategory != StatusCategory.Done)
                .OrderBy(issue => issue.BoardPosition)
                .ToList();
        }

        return column.Issues.OrderBy(issue => issue.BoardPosition).ToList();
    }

    private int GetBoardModeTotalCount(BoardColumnDto column) => GetBoardModeIssues(column).Count;

    private static bool IssueMatchesFilters(IssueSummaryDto issue, string? assignee, string? priority, string? type, string search)
    {
        return (string.IsNullOrWhiteSpace(assignee) || issue.AssigneeNames.Contains(assignee)) &&
               (string.IsNullOrWhiteSpace(priority) || string.Equals(issue.Priority.ToString(), priority, StringComparison.OrdinalIgnoreCase)) &&
               (string.IsNullOrWhiteSpace(type) || string.Equals(issue.Type.ToString(), type, StringComparison.OrdinalIgnoreCase)) &&
               (string.IsNullOrWhiteSpace(search) ||
                issue.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                // issue.IssueKey.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(issue.EpicTitle) && issue.EpicTitle.Contains(search, StringComparison.OrdinalIgnoreCase)));
    }

    private void ClearFilters()
    {
        _suppressFilterEvents = true;
        try
        {
            if (_assigneeFilter.Items.Count > 0)
            {
                _assigneeFilter.SelectedIndex = 0;
            }

            if (_priorityFilter.Items.Count > 0)
            {
                _priorityFilter.SelectedIndex = 0;
            }

            if (_typeFilter.Items.Count > 0)
            {
                _typeFilter.SelectedIndex = 0;
            }

            _searchFilter.Clear();
        }
        finally
        {
            _suppressFilterEvents = false;
        }

        ApplyFilters();
    }

    private void RenderColumns(IReadOnlyList<BoardColumnDto> columns)
    {
        _swimlanesPanel.Visible = false;
        _boardColumnsPanel.Visible = true;
        _boardColumnsPanel.SuspendLayout();
        var totalWidth = 0;

        foreach (var column in columns)
        {
            var control = GetOrCreateColumnControl(column.StatusId, column);
            control.Visible = true;
            control.Bind(column);
            totalWidth += control.Width + control.Margin.Horizontal;
        }

        _boardColumnsPanel.ResumeLayout();
        UpdateBoardScrollMetrics(totalWidth);
    }

    private void RenderSwimlanes(IReadOnlyList<EpicSwimlaneViewModel> lanes, int? animatedIssueId = null)
    {
        _boardColumnsPanel.Visible = false;
        _swimlanesPanel.Visible = true;
        _swimlanesPanel.SuspendLayout();
        try
        {
            DetachAndDisposeSwimlanes();
            _swimlanesPanel.Controls.Clear();
            foreach (var lane in lanes)
            {
                var control = new EpicSwimlaneControl(lane, showStoryPointProgress: !_activeSprintOnly)
                {
                    Width = Math.Max(320, lane.Columns.Count * 312)
                };
                control.Bind(lane, animatedIssueId, !_activeSprintOnly);
                AttachSwimlaneControl(control);
                _swimlanesPanel.Controls.Add(control);
            }
        }
        finally
        {
            _swimlanesPanel.ResumeLayout();
        }

        var totalWidth = lanes.Count == 0 ? _loadedColumns.Count * 312 : lanes.Max(lane => lane.Columns.Count) * 312;
        UpdateBoardScrollMetrics(totalWidth);
    }

    private IReadOnlyList<EpicSwimlaneViewModel> BuildSwimlanes(IReadOnlyList<BoardColumnDto> columns)
    {
        var allIssues = columns.SelectMany(column => column.Issues).ToList();
        var workItems = allIssues.Where(issue => issue.Type != IssueType.Epic).ToList();
        var metadata = new Dictionary<string, (int? EpicId, string Title, Color Color)>();

        foreach (var issue in allIssues)
        {
            if (issue.Type == IssueType.Epic)
            {
                var laneKey = BuildLaneKey(issue.Id);
                metadata[laneKey] = (issue.Id, BuildEpicTitle(issue), ParseColor(issue.EpicColor, JiraTheme.Blue600));
            }
            else if (issue.EpicId.HasValue)
            {
                var laneKey = BuildLaneKey(issue.EpicId.Value);
                metadata[laneKey] = (issue.EpicId, BuildEpicTitle(issue), ParseColor(issue.EpicColor, JiraTheme.Blue600));
            }
        }

        var groupedIssues = workItems
            .GroupBy(issue => issue.EpicId.HasValue ? BuildLaneKey(issue.EpicId.Value) : NoEpicLaneKey)
            .ToDictionary(group => group.Key, group => group.OrderBy(issue => issue.BoardPosition).ToList());

        var laneKeys = metadata.Keys
            .Union(groupedIssues.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => string.Equals(key, NoEpicLaneKey, StringComparison.Ordinal) ? 1 : 0)
            .ThenBy(key => metadata.TryGetValue(key, out var entry) ? entry.Title : "Không có Epic", StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lanes = new List<EpicSwimlaneViewModel>();
        foreach (var laneKey in laneKeys)
        {
            var laneIssues = groupedIssues.GetValueOrDefault(laneKey, []);
            if (string.Equals(laneKey, NoEpicLaneKey, StringComparison.Ordinal) && laneIssues.Count == 0)
            {
                continue;
            }

            var laneMeta = metadata.GetValueOrDefault(laneKey, (null, "Không có Epic", JiraTheme.Neutral500));
            var laneColumns = columns
                .OrderBy(column => column.DisplayOrder)
                .Select(column => new EpicSwimlaneColumnViewModel(
                    column.StatusId,
                    column.Name,
                    column.Color,
                    column.WipLimit,
                    column.TotalIssueCount,
                    laneIssues.Where(issue => issue.StatusId == column.StatusId).OrderBy(issue => issue.BoardPosition).ToList()))
                .ToList();

            var doneIssues = laneIssues.Count(issue => issue.StatusCategory == StatusCategory.Done);
            var totalIssues = laneIssues.Count;
            var doneStoryPoints = laneIssues.Where(issue => issue.StatusCategory == StatusCategory.Done).Sum(issue => issue.StoryPoints ?? 0);
            var totalStoryPoints = laneIssues.Sum(issue => issue.StoryPoints ?? 0);
            lanes.Add(new EpicSwimlaneViewModel(
                laneKey,
                laneMeta.Item1,
                laneMeta.Title,
                laneMeta.Item3,
                _collapsedLaneKeys.Contains(laneKey),
                doneIssues,
                totalIssues,
                doneStoryPoints,
                totalStoryPoints,
                laneColumns));
        }

        if (lanes.Count == 0)
        {
            lanes.Add(new EpicSwimlaneViewModel(NoEpicLaneKey, null, "Không có Epic", JiraTheme.Neutral500, false, 0, 0, 0, 0,
                columns.OrderBy(column => column.DisplayOrder)
                    .Select(column => new EpicSwimlaneColumnViewModel(column.StatusId, column.Name, column.Color, column.WipLimit, column.TotalIssueCount, []))
                    .ToList()));
        }

        return lanes;
    }

    private static string BuildEpicTitle(IssueSummaryDto issue)
    {
        var issueKey = issue.EpicKey ?? issue.IssueKey;
        var title = issue.EpicTitle ?? issue.Title;
        return string.IsNullOrWhiteSpace(issueKey) ? title : $"{issueKey} - {title}";
    }

    private static string BuildLaneKey(int epicId) => $"epic:{epicId}";

    private void OnLaneCollapseChanged(EpicSwimlaneCollapseChangedEventArgs args)
    {
        if (args.IsCollapsed)
        {
            _collapsedLaneKeys.Add(args.LaneKey);
        }
        else
        {
            _collapsedLaneKeys.Remove(args.LaneKey);
        }

        ApplyFilters();
    }

    private async Task ToggleBoardModeAsync(CancellationToken cancellationToken = default)
    {
        if (_project is null)
        {
            return;
        }

        using var operationCts = CreateOperationSource(cancellationToken);
        var token = operationCts.Token;

        try
        {
            var nextBoardType = _boardType == BoardType.Scrum ? BoardType.Kanban : BoardType.Scrum;
            var updatedProject = await _session.ProjectCommands.UpdateProjectAsync(
                _project.Id,
                _project.Name,
                _project.Description,
                _project.Category,
                nextBoardType,
                _project.Url,
                token);
            if (updatedProject is null)
            {
                return;
            }

            _project = updatedProject;
            _boardType = updatedProject.BoardType;
            UpdateBoardModeButton();
            await ReloadBoardAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private void ToggleGroupByEpic()
    {
        _groupByEpic = !_groupByEpic;
        UpdateGroupByEpicButton();
        ApplyFilters();
    }

    private void UpdateGroupByEpicButton()
    {
        _groupByEpicButton.BackColor = _groupByEpic ? JiraTheme.Blue100 : JiraTheme.BgSurface;
        _groupByEpicButton.ForeColor = _groupByEpic ? JiraTheme.PrimaryActive : JiraTheme.TextPrimary;
        _groupByEpicButton.FlatAppearance.BorderSize = 1;
        _groupByEpicButton.FlatAppearance.BorderColor = _groupByEpic ? JiraTheme.Primary : JiraTheme.Border;
        _groupByEpicButton.FlatAppearance.MouseDownBackColor = _groupByEpic ? JiraTheme.Blue100 : JiraTheme.Neutral200;
        _groupByEpicButton.FlatAppearance.MouseOverBackColor = _groupByEpic ? JiraTheme.Blue100 : JiraTheme.Neutral100;
    }

    private void UpdateBoardModeButton()
    {
        var isKanban = _boardType == BoardType.Kanban;
        _boardModeButton.Visible = _canChangeBoardMode;
        _boardModeButton.Text = isKanban ? "Chế độ: Kanban" : "Chế độ: Scrum";
        _boardModeButton.BackColor = isKanban ? JiraTheme.Blue100 : JiraTheme.BgSurface;
        _boardModeButton.ForeColor = isKanban ? JiraTheme.PrimaryActive : JiraTheme.TextPrimary;
        _boardModeButton.FlatAppearance.BorderSize = 1;
        _boardModeButton.FlatAppearance.BorderColor = isKanban ? JiraTheme.Primary : JiraTheme.Border;
        _boardModeButton.FlatAppearance.MouseDownBackColor = isKanban ? JiraTheme.Blue100 : JiraTheme.Neutral200;
        _boardModeButton.FlatAppearance.MouseOverBackColor = isKanban ? JiraTheme.Blue100 : JiraTheme.Neutral100;
    }

    private BoardColumnControl GetOrCreateColumnControl(int statusId, BoardColumnDto column)
    {
        if (_columnControls.TryGetValue(statusId, out var existing))
        {
            return existing;
        }

        var control = new BoardColumnControl(column)
        {
            Margin = new Padding(0, 0, 16, 0),
            Height = Math.Max(200, _boardScrollPanel.ClientSize.Height - 8),
        };
        AttachColumnControl(control);
        _columnControls[statusId] = control;
        _boardColumnsPanel.Controls.Add(control);
        return control;
    }

    private void AttachColumnControl(BoardColumnControl control)
    {
        control.IssueSelected += OnColumnIssueSelected;
        control.CreateIssueRequested += OnColumnCreateIssueRequested;
        control.IssueMoveRequested += OnColumnIssueMoveRequested;
        control.WipLimitWarningRequested += OnWipLimitWarningRequested;
    }

    private void DetachColumnControl(BoardColumnControl control)
    {
        control.IssueSelected -= OnColumnIssueSelected;
        control.CreateIssueRequested -= OnColumnCreateIssueRequested;
        control.IssueMoveRequested -= OnColumnIssueMoveRequested;
        control.WipLimitWarningRequested -= OnWipLimitWarningRequested;
    }

    private void AttachSwimlaneControl(EpicSwimlaneControl control)
    {
        control.IssueSelected += OnSwimlaneIssueSelected;
        control.EpicSelected += OnSwimlaneEpicSelected;
        control.IssueMoveRequested += OnSwimlaneIssueMoveRequested;
        control.CollapseChanged += OnSwimlaneCollapseChanged;
    }

    private void DetachSwimlaneControl(EpicSwimlaneControl control)
    {
        control.IssueSelected -= OnSwimlaneIssueSelected;
        control.EpicSelected -= OnSwimlaneEpicSelected;
        control.IssueMoveRequested -= OnSwimlaneIssueMoveRequested;
        control.CollapseChanged -= OnSwimlaneCollapseChanged;
    }

    private void DetachAndDisposeSwimlanes()
    {
        foreach (Control control in _swimlanesPanel.Controls)
        {
            if (control is EpicSwimlaneControl swimlane)
            {
                DetachSwimlaneControl(swimlane);
            }

            control.Dispose();
        }
    }

    private void OnWipLimitWarningRequested(object? sender, BoardColumnWipLimitEventArgs args)
    {
        ShowWarningToast($"Cột {IssueDisplayText.TranslateStatus(args.StatusName)} đã chạm giới hạn WIP ({args.CurrentCount}/{args.Limit}). Thả để xác nhận ghi đè.");
    }

    private void RenderMovedColumns(IReadOnlyCollection<int> statusIds, int issueId, int targetStatusId)
    {
        foreach (var statusId in statusIds.Distinct())
        {
            var column = _loadedColumns.FirstOrDefault(x => x.StatusId == statusId);
            if (column is null)
            {
                continue;
            }

            var filteredColumn = GetFilteredColumn(column);
            var control = GetOrCreateColumnControl(statusId, filteredColumn);
            control.Bind(filteredColumn, statusId == targetStatusId ? issueId : null);
        }
    }

    private void UpdateBoardScrollMetrics(int totalWidth)
    {
        _boardScrollPanel.AutoScrollMinSize = new Size(Math.Max(_boardScrollPanel.ClientSize.Width, totalWidth + 16), _boardScrollPanel.ClientSize.Height - 8);
    }

    private void UpdateColumnHeights()
    {
        if (_groupByEpic)
        {
            UpdateBoardScrollMetrics(_loadedColumns.Count * 312);
            return;
        }

        foreach (var control in _columnControls.Values)
        {
            control.Height = Math.Max(200, _boardScrollPanel.ClientSize.Height - 8);
        }

        UpdateBoardScrollMetrics(_columnControls.Values.Sum(control => control.Width + control.Margin.Horizontal));
    }

    private async Task StartSprintAsync(CancellationToken cancellationToken = default)
    {
        using var operationCts = CreateOperationSource(cancellationToken);
        var token = operationCts.Token;

        try
        {
            if (_boardType == BoardType.Kanban || _projectId == 0 || _activeSprint is not null)
            {
                return;
            }

            var nextSprint = (await _session.Sprints.GetByProjectAsync(_projectId, token))
                .Where(x => x.State == SprintState.Planned)
                .OrderBy(x => x.StartDate ?? DateOnly.MaxValue)
                .ThenBy(x => x.Name)
                .FirstOrDefault();

            if (nextSprint is null)
            {
                ErrorDialogService.Show("Không có sprint đã lập kế hoạch nào để bắt đầu.");
                return;
            }

            await _session.Sprints.StartSprintAsync(nextSprint.Id, token);
            await ReloadBoardAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task CreateIssueAsync(int? defaultStatusId = null, int? epicParentIssueId = null, IssueType? preferredType = null)
    {
        try
        {
            using var dialog = new IssueEditorForm(_session, _projectId, null, defaultStatusId, epicParentIssueId, preferredType);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                await ReloadBoardAsync(_disposeCts.Token);
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

    private async Task OpenIssueDetailsAsync(int issueId, bool openChildIssues = false)
    {
        try
        {
            using var dialog = new IssueDetailsForm(_session, issueId, _projectId, openChildIssues);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                await ReloadBoardAsync(_disposeCts.Token);
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

    private async Task MoveIssueAsync(IssueMoveRequestedEventArgs args, CancellationToken cancellationToken = default)
    {
        using var operationCts = CreateOperationSource(cancellationToken);
        var token = operationCts.Token;

        try
        {
            if (args.SourceStatusId == args.TargetStatusId)
            {
                return;
            }

            var currentUserId = _session.CurrentUserContext.RequireUserId();
            var targetColumn = _loadedColumns.FirstOrDefault(x => x.StatusId == args.TargetStatusId);
            if (targetColumn is null)
            {
                return;
            }

            if (ShouldConfirmWipOverride(targetColumn))
            {
                var currentCount = GetBoardModeTotalCount(targetColumn);
                var confirm = MessageBox.Show(
                    this,
                    $"{targetColumn.Name} đã đạt giới hạn WIP ({currentCount}/{targetColumn.WipLimit}). Vẫn chuyển công việc?",
                    "Đã đạt giới hạn WIP",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes)
                {
                    ShowWarningToast($"Đã hủy chuyển vì {targetColumn.Name} đã đạt giới hạn WIP.");
                    return;
                }
            }

            var boardPosition = targetColumn.Issues.OrderBy(x => x.BoardPosition).FirstOrDefault() is { } first
                ? first.BoardPosition - 1m
                : 1m;

            var moved = await _session.RunSerializedAsync(() =>
                _session.Issues.MoveAsync(args.IssueId, args.TargetStatusId, boardPosition, currentUserId, token), token);

            if (!moved)
            {
                return;
            }

            ApplyIssueMoveLocally(args.IssueId, args.SourceStatusId, args.TargetStatusId, boardPosition);
            ShowMoveToast(args.TargetStatusName);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private bool ShouldConfirmWipOverride(BoardColumnDto targetColumn)

    {
        return targetColumn.Category != StatusCategory.Done
            && targetColumn.WipLimit is int wipLimit
            && wipLimit > 0
            && GetBoardModeTotalCount(targetColumn) >= wipLimit;
    }

    private void ApplyIssueMoveLocally(int issueId, int sourceStatusId, int targetStatusId, decimal boardPosition)
    {
        if (sourceStatusId == targetStatusId)
        {
            return;
        }

        var sourceColumn = _loadedColumns.FirstOrDefault(x => x.StatusId == sourceStatusId);
        var targetColumn = _loadedColumns.FirstOrDefault(x => x.StatusId == targetStatusId);
        if (sourceColumn is null || targetColumn is null)
        {
            return;
        }

        var issue = sourceColumn.Issues.FirstOrDefault(x => x.Id == issueId);
        if (issue is null)
        {
            return;
        }

        var updatedIssue = issue with
        {
            StatusId = targetStatusId,
            StatusName = targetColumn.Name,
            StatusColor = targetColumn.Color,
            StatusCategory = targetColumn.Category,
            BoardPosition = boardPosition,
        };

        _loadedColumns = _loadedColumns
            .Select(column =>
            {
                if (column.StatusId == sourceStatusId)
                {
                    return column with
                    {
                        Issues = column.Issues
                            .Where(x => x.Id != issueId)
                            .OrderBy(x => x.BoardPosition)
                            .ToList(),
                        TotalIssueCount = Math.Max(0, column.TotalIssueCount - 1)
                    };
                }

                if (column.StatusId == targetStatusId)
                {
                    return column with
                    {
                        Issues = column.Issues
                            .Where(x => x.Id != issueId)
                            .Append(updatedIssue)
                            .OrderBy(x => x.BoardPosition)
                            .ToList(),
                        TotalIssueCount = column.TotalIssueCount + 1
                    };
                }

                return column;
            })
            .ToList();

        if (_groupByEpic)
        {
            RenderSwimlanes(BuildSwimlanes(_loadedColumns.Select(GetFilteredColumn).ToList()), issueId);
        }
        else
        {
            RenderMovedColumns(new[] { sourceStatusId, targetStatusId }, issueId, targetStatusId);
        }
    }

    private void ShowMoveToast(string statusName)
    {
        ShowToast($"Đã chuyển issue sang {IssueDisplayText.TranslateStatus(statusName)}", JiraTheme.Blue600);
    }

    private void ShowWarningToast(string message)
    {
        ShowToast(message, JiraTheme.Red500);
    }

    private void ShowToast(string message, Color accentColor)
    {
        _toastLabel.Text = message;
        _toastAccentColor = accentColor;
        PositionToast();
        _toastPanel.Visible = true;
        _toastPanel.BringToFront();
        _toastPanel.Invalidate();
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void HideToast()
    {
        _toastTimer.Stop();
        _toastPanel.Visible = false;
    }

    private void PositionToast()
    {
        _toastPanel.Location = new Point(
            Math.Max(16, Width - _toastPanel.Width - 24),
            80 + 72 + 12);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelPendingLoad();
            _disposeCts.Cancel();
            Load -= OnBoardLoad;
            Resize -= OnBoardResize;
            _startSprintButton.Click -= OnStartSprintButtonClick;
            _boardModeButton.Click -= OnBoardModeButtonClick;
            _assigneeFilter.SelectedIndexChanged -= OnFilterChanged;
            _priorityFilter.SelectedIndexChanged -= OnFilterChanged;
            _typeFilter.SelectedIndexChanged -= OnFilterChanged;
            _searchFilter.TextChanged -= OnFilterChanged;
            _groupByEpicButton.Click -= OnGroupByEpicButtonClick;
            _clearFiltersButton.Click -= OnClearFiltersButtonClick;
            _toastPanel.Paint -= OnToastPanelPaint;
            _topBar.Paint -= OnTopBarPaint;
            _filterBar.Paint -= OnFilterBarPaint;
            _toastTimer.Tick -= OnToastTimerTick;
            foreach (var control in _columnControls.Values)
            {
                DetachColumnControl(control);
            }

            DetachAndDisposeSwimlanes();
            _toastTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private async void OnBoardLoad(object? sender, EventArgs e)
    {
        await ReloadBoardAsync(_disposeCts.Token);
    }

    private void OnBoardResize(object? sender, EventArgs e)
    {
        UpdateColumnHeights();
        PositionToast();
    }

    private async void OnStartSprintButtonClick(object? sender, EventArgs e)
    {
        await StartSprintAsync(_disposeCts.Token);
    }

    private async void OnBoardModeButtonClick(object? sender, EventArgs e)
    {
        await ToggleBoardModeAsync(_disposeCts.Token);
    }

    private void OnFilterChanged(object? sender, EventArgs e)
    {
        if (_suppressFilterEvents)
        {
            return;
        }

        ApplyFilters();
    }

    private void OnGroupByEpicButtonClick(object? sender, EventArgs e)
    {
        ToggleGroupByEpic();
    }

    private void OnClearFiltersButtonClick(object? sender, EventArgs e)
    {
        ClearFilters();
    }

    private void OnToastPanelPaint(object? sender, PaintEventArgs e)
    {
        using var accentBrush = new SolidBrush(_toastAccentColor);
        using var borderPen = new Pen(JiraTheme.Border);
        e.Graphics.FillRectangle(accentBrush, 0, 0, 4, _toastPanel.Height);
        e.Graphics.DrawRectangle(borderPen, 0, 0, _toastPanel.Width - 1, _toastPanel.Height - 1);
    }

    private void OnToastTimerTick(object? sender, EventArgs e)
    {
        HideToast();
    }

    private void OnTopBarPaint(object? sender, PaintEventArgs e)
    {
        using var pen = new Pen(JiraTheme.Border);
        e.Graphics.DrawLine(pen, 0, _topBar.Height - 1, _topBar.Width, _topBar.Height - 1);
    }

    private void OnFilterBarPaint(object? sender, PaintEventArgs e)
    {
        using var pen = new Pen(JiraTheme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, _filterBar.Width - 1, _filterBar.Height - 1);
    }

    private async void OnSwimlaneIssueSelected(object? sender, int issueId)
    {
        await OpenIssueDetailsAsync(issueId);
    }

    private async void OnSwimlaneEpicSelected(object? sender, int epicId)
    {
        await OpenIssueDetailsAsync(epicId, openChildIssues: true);
    }

    private async void OnSwimlaneIssueMoveRequested(object? sender, IssueMoveRequestedEventArgs args)
    {
        await MoveIssueAsync(args, _disposeCts.Token);
    }

    private void OnSwimlaneCollapseChanged(object? sender, EpicSwimlaneCollapseChangedEventArgs args)
    {
        OnLaneCollapseChanged(args);
    }

    private async void OnColumnIssueSelected(object? sender, int issueId)
    {
        await OpenIssueDetailsAsync(issueId);
    }

    private async void OnColumnCreateIssueRequested(object? sender, int issueStatusId)
    {
        await CreateIssueAsync(issueStatusId);
    }

    private async void OnColumnIssueMoveRequested(object? sender, IssueMoveRequestedEventArgs args)
    {
        await MoveIssueAsync(args, _disposeCts.Token);
    }

    private static Color ParseColor(string? value, Color fallback)
    {
        try
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : ColorTranslator.FromHtml(value);
        }
        catch
        {
            return fallback;
        }
    }

    private const string NoEpicLaneKey = "no-epic";
}