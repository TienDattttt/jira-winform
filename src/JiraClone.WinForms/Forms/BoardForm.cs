using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Controls;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public class BoardForm : UserControl
{
    private readonly AppSession _session;
    private readonly bool _activeSprintOnly;
    private readonly Label _sprintTitleLabel = JiraControlFactory.CreateLabel("No active sprint");
    private readonly Label _sprintDateLabel = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly Button _startSprintButton = JiraControlFactory.CreatePrimaryButton("Start Sprint");
    private readonly ComboBox _assigneeFilter = CreateFilterCombo(150);
    private readonly ComboBox _priorityFilter = CreateFilterCombo(130);
    private readonly ComboBox _typeFilter = CreateFilterCombo(130);
    private readonly TextBox _searchFilter = JiraControlFactory.CreateTextBox();
    private readonly Button _clearFiltersButton = JiraControlFactory.CreateSecondaryButton("Clear filters");
    private readonly Dictionary<IssueStatus, BoardColumnControl> _columnControls = new();
    private readonly Panel _toastPanel = new();
    private readonly Label _toastLabel = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly System.Windows.Forms.Timer _toastTimer = new() { Interval = 2600 };
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
    private readonly Panel _boardScrollPanel = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        BackColor = JiraTheme.BgPage,
        Padding = new Padding(16, 12, 16, 16),
    };

    private int _projectId;
    private Sprint? _activeSprint;
    private IReadOnlyList<BoardColumnDto> _loadedColumns = Array.Empty<BoardColumnDto>();
    private bool _isLoadingBoard;

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
        _startSprintButton.Size = new Size(144, 38);
        _startSprintButton.Click += async (_, _) => await StartSprintAsync();

        _searchFilter.Width = 260;
        _searchFilter.PlaceholderText = "Search issues";
        _clearFiltersButton.AutoSize = false;
        _clearFiltersButton.Size = new Size(124, 38);
        _assigneeFilter.Margin = new Padding(0, 0, 12, 0);
        _priorityFilter.Margin = new Padding(0, 0, 12, 0);
        _typeFilter.Margin = new Padding(0, 0, 12, 0);
        _searchFilter.Margin = new Padding(0, 0, 12, 0);
        _clearFiltersButton.Margin = new Padding(0);

        _assigneeFilter.SelectedIndexChanged += (_, _) => ApplyFilters();
        _priorityFilter.SelectedIndexChanged += (_, _) => ApplyFilters();
        _typeFilter.SelectedIndexChanged += (_, _) => ApplyFilters();
        _searchFilter.TextChanged += (_, _) => ApplyFilters();
        _clearFiltersButton.Click += (_, _) => ClearFilters();

        ConfigureToast();

        _boardScrollPanel.Controls.Add(_boardColumnsPanel);

        Controls.Add(_toastPanel);
        Controls.Add(_boardScrollPanel);
        Controls.Add(BuildFilterBar());
        Controls.Add(BuildTopBar());

        Load += async (_, _) => await LoadBoardAsync();
        Resize += (_, _) =>
        {
            UpdateColumnHeights();
            PositionToast();
        };
    }

    public Task RefreshBoardAsync() => LoadBoardAsync();

    public void SetShellSearch(string searchText)
    {
        var value = searchText ?? string.Empty;
        if (string.Equals(_searchFilter.Text, value, StringComparison.Ordinal))
        {
            return;
        }

        _searchFilter.Text = value;
        ApplyFilters();
    }

    private void ConfigureToast()
    {
        _toastPanel.Visible = false;
        _toastPanel.Size = new Size(300, 56);
        _toastPanel.BackColor = JiraTheme.BgSurface;
        _toastPanel.Padding = new Padding(14, 10, 14, 10);

        _toastLabel.Dock = DockStyle.Fill;
        _toastLabel.Font = JiraTheme.FontSmall;
        _toastLabel.ForeColor = JiraTheme.TextPrimary;

        _toastPanel.Controls.Add(_toastLabel);
        _toastPanel.Paint += (_, e) =>
        {
            using var accentBrush = new SolidBrush(JiraTheme.Blue600);
            using var borderPen = new Pen(JiraTheme.Border);
            e.Graphics.FillRectangle(accentBrush, 0, 0, 4, _toastPanel.Height);
            e.Graphics.DrawRectangle(borderPen, 0, 0, _toastPanel.Width - 1, _toastPanel.Height - 1);
        };

        _toastTimer.Tick += (_, _) => HideToast();
    }

    private Control BuildTopBar()
    {
        var topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(16, 10, 16, 10),
        };

        topBar.Paint += (_, e) =>
        {
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawLine(pen, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);
        };

        var right = new Panel
        {
            Dock = DockStyle.Right,
            Width = 176,
            BackColor = JiraTheme.BgSurface,
        };
        right.Controls.Add(_startSprintButton);
        _startSprintButton.Location = new Point(16, 11);

        var left = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
        };
        left.Controls.Add(_sprintTitleLabel);
        left.Controls.Add(_sprintDateLabel);
        _sprintTitleLabel.Location = new Point(0, 0);
        _sprintDateLabel.Location = new Point(0, 34);

        topBar.Controls.Add(right);
        topBar.Controls.Add(left);
        return topBar;
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

        var filterBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(12, 6, 12, 6),
            WrapContents = false,
            Margin = new Padding(0),
        };
        filterBar.Paint += (_, e) =>
        {
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, filterBar.Width - 1, filterBar.Height - 1);
        };

        filterBar.Controls.Add(_assigneeFilter);
        filterBar.Controls.Add(_priorityFilter);
        filterBar.Controls.Add(_typeFilter);
        filterBar.Controls.Add(_searchFilter);
        filterBar.Controls.Add(_clearFiltersButton);
        host.Controls.Add(filterBar);
        return host;
    }

    private static ComboBox CreateFilterCombo(int width) => new()
    {
        Width = width,
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        BackColor = JiraTheme.BgSurface,
        ForeColor = JiraTheme.TextPrimary,
        Font = JiraTheme.FontBody,
        IntegralHeight = false
    };

    private async Task LoadBoardAsync()
    {
        if (_isLoadingBoard || !Visible)
        {
            return;
        }

        try
        {
            _isLoadingBoard = true;
            Project? project = null;
            Sprint? activeSprint = null;
            IReadOnlyList<BoardColumnDto> columns = Array.Empty<BoardColumnDto>();

            await _session.RunSerializedAsync(async () =>
            {
                project = await _session.Projects.GetActiveProjectAsync();
                if (project is null)
                {
                    return;
                }

                activeSprint = await _session.Sprints.GetActiveByProjectAsync(project.Id);
                columns = _activeSprintOnly
                    ? await _session.Board.GetBoardAsync(project.Id, activeSprint?.Id)
                    : await _session.Board.GetBoardAsync(project.Id);
            });

            if (project is null)
            {
                ErrorDialogService.Show("No active project was found.");
                return;
            }

            _projectId = project.Id;
            _activeSprint = activeSprint;
            _loadedColumns = columns;
            PopulateHeader();
            PopulateFilterOptions(_loadedColumns);
            ApplyFilters();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
        finally
        {
            _isLoadingBoard = false;
        }
    }

    private void PopulateHeader()
    {
        if (_activeSprint is null)
        {
            _sprintTitleLabel.Text = _activeSprintOnly ? "No active sprint" : "Backlog";
            _sprintDateLabel.Text = _activeSprintOnly ? "Start a planned sprint to focus the board" : "Showing all project issues";
            _startSprintButton.Enabled = _activeSprintOnly;
            _startSprintButton.Visible = _activeSprintOnly;
            _startSprintButton.Text = "Start Sprint";
            return;
        }

        _sprintTitleLabel.Text = _activeSprintOnly ? _activeSprint.Name : "Backlog";
        _sprintDateLabel.Text = _activeSprintOnly ? FormatSprintDateRange(_activeSprint) : $"Active sprint: {_activeSprint.Name}";
        _startSprintButton.Enabled = false;
        _startSprintButton.Visible = _activeSprintOnly;
        _startSprintButton.Text = "Running";
    }

    private static string FormatSprintDateRange(Sprint sprint)
    {
        if (!sprint.StartDate.HasValue && !sprint.EndDate.HasValue)
        {
            return "No date range";
        }

        var start = sprint.StartDate?.ToString("dd MMM yyyy") ?? "?";
        var end = sprint.EndDate?.ToString("dd MMM yyyy") ?? "?";
        return $"{start} - {end}";
    }

    private void PopulateFilterOptions(IReadOnlyList<BoardColumnDto> columns)
    {
        var issues = columns.SelectMany(x => x.Issues).ToList();
        var assignees = issues.SelectMany(x => x.AssigneeNames).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

        ResetCombo(_assigneeFilter, "All assignees", assignees);
        ResetCombo(_priorityFilter, "All priorities", Enum.GetNames<IssuePriority>());
        ResetCombo(_typeFilter, "All types", Enum.GetNames<IssueType>());
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
        RenderColumns(_loadedColumns.Select(GetFilteredColumn).ToList());
    }

    private BoardColumnDto GetFilteredColumn(BoardColumnDto column)
    {
        var assignee = _assigneeFilter.SelectedItem as string;
        var priority = _priorityFilter.SelectedItem as string;
        var type = _typeFilter.SelectedItem as string;
        var search = _searchFilter.Text.Trim();

        var issues = column.Issues
            .Where(issue => IssueMatchesFilters(issue, assignee, priority, type, search))
            .OrderBy(issue => issue.BoardPosition)
            .ToList();

        return column with { Issues = issues };
    }

    private static bool IssueMatchesFilters(IssueSummaryDto issue, string? assignee, string? priority, string? type, string search)
    {
        return (string.IsNullOrWhiteSpace(assignee) || assignee.StartsWith("All ") || issue.AssigneeNames.Contains(assignee)) &&
               (string.IsNullOrWhiteSpace(priority) || priority.StartsWith("All ") || string.Equals(issue.Priority.ToString(), priority, StringComparison.OrdinalIgnoreCase)) &&
               (string.IsNullOrWhiteSpace(type) || type.StartsWith("All ") || string.Equals(issue.Type.ToString(), type, StringComparison.OrdinalIgnoreCase)) &&
               (string.IsNullOrWhiteSpace(search) ||
                issue.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                issue.IssueKey.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private void ClearFilters()
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
        ApplyFilters();
    }

    private void RenderColumns(IReadOnlyList<BoardColumnDto> columns)
    {
        _boardColumnsPanel.SuspendLayout();
        var totalWidth = 0;

        foreach (var column in columns)
        {
            var control = GetOrCreateColumnControl(column.Status, column);
            control.Bind(column);
            totalWidth += control.Width + control.Margin.Horizontal;
        }

        _boardColumnsPanel.ResumeLayout();
        UpdateBoardScrollMetrics(totalWidth);
    }

    private BoardColumnControl GetOrCreateColumnControl(IssueStatus status, BoardColumnDto column)
    {
        if (_columnControls.TryGetValue(status, out var existing))
        {
            return existing;
        }

        var control = new BoardColumnControl(column)
        {
            Margin = new Padding(0, 0, 16, 0),
            Height = Math.Max(200, _boardScrollPanel.ClientSize.Height - 8),
        };
        control.IssueSelected += async (_, issueId) => await OpenIssueDetailsAsync(issueId);
        control.CreateIssueRequested += async (_, issueStatus) => await CreateIssueAsync(issueStatus);
        control.IssueMoveRequested += async (_, args) => await MoveIssueAsync(args);
        _columnControls[status] = control;
        _boardColumnsPanel.Controls.Add(control);
        return control;
    }

    private void RenderMovedColumns(IReadOnlyCollection<IssueStatus> statuses, int issueId, IssueStatus targetStatus)
    {
        foreach (var status in statuses.Distinct())
        {
            var column = _loadedColumns.FirstOrDefault(x => x.Status == status);
            if (column is null)
            {
                continue;
            }

            var filteredColumn = GetFilteredColumn(column);
            var control = GetOrCreateColumnControl(status, filteredColumn);
            control.Bind(filteredColumn, status == targetStatus ? issueId : null);
        }
    }

    private void UpdateBoardScrollMetrics(int totalWidth)
    {
        _boardScrollPanel.AutoScrollMinSize = new Size(Math.Max(_boardScrollPanel.ClientSize.Width, totalWidth + 16), _boardScrollPanel.ClientSize.Height - 8);
    }

    private void UpdateColumnHeights()
    {
        foreach (var control in _columnControls.Values)
        {
            control.Height = Math.Max(200, _boardScrollPanel.ClientSize.Height - 8);
        }

        UpdateBoardScrollMetrics(_columnControls.Values.Sum(control => control.Width + control.Margin.Horizontal));
    }

    private async Task StartSprintAsync()
    {
        try
        {
            if (_projectId == 0 || _activeSprint is not null)
            {
                return;
            }

            var nextSprint = (await _session.Sprints.GetByProjectAsync(_projectId))
                .Where(x => x.State == SprintState.Planned)
                .OrderBy(x => x.StartDate ?? DateOnly.MaxValue)
                .ThenBy(x => x.Name)
                .FirstOrDefault();

            if (nextSprint is null)
            {
                ErrorDialogService.Show("No planned sprint is available to start.");
                return;
            }

            await _session.Sprints.StartSprintAsync(nextSprint.Id);
            await LoadBoardAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task CreateIssueAsync(IssueStatus? defaultStatus = null)
    {
        try
        {
            using var dialog = new IssueEditorForm(_session, _projectId, null, defaultStatus);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                await LoadBoardAsync();
            }
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task OpenIssueDetailsAsync(int issueId)
    {
        try
        {
            using var dialog = new IssueDetailsForm(_session, issueId, _projectId);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                await LoadBoardAsync();
            }
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task MoveIssueAsync(IssueMoveRequestedEventArgs args)
    {
        try
        {
            if (args.SourceStatus == args.TargetStatus)
            {
                return;
            }

            var currentUserId = _session.CurrentUserContext.RequireUserId();
            var targetColumn = _loadedColumns.FirstOrDefault(x => x.Status == args.TargetStatus);
            var boardPosition = targetColumn?.Issues.OrderBy(x => x.BoardPosition).FirstOrDefault() is { } first
                ? first.BoardPosition - 1m
                : 1m;

            var moved = await _session.RunSerializedAsync(async () =>
            {
                await using var dbContext = _session.CreateDbContext();
                return await _session.CreateIssueService(dbContext).UpdateStatusAsync(args.IssueId, args.TargetStatus, boardPosition, currentUserId);
            });

            if (!moved)
            {
                return;
            }

            ApplyIssueMoveLocally(args.IssueId, args.SourceStatus, args.TargetStatus, boardPosition);
            ShowMoveToast(args.TargetStatus);
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private void ApplyIssueMoveLocally(int issueId, IssueStatus sourceStatus, IssueStatus targetStatus, decimal boardPosition)
    {
        if (sourceStatus == targetStatus)
        {
            return;
        }

        var sourceColumn = _loadedColumns.FirstOrDefault(x => x.Status == sourceStatus);
        if (sourceColumn is null)
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
            Status = targetStatus,
            BoardPosition = boardPosition,
        };

        _loadedColumns = _loadedColumns
            .Select(column =>
            {
                if (column.Status == sourceStatus)
                {
                    return column with
                    {
                        Issues = column.Issues
                            .Where(x => x.Id != issueId)
                            .OrderBy(x => x.BoardPosition)
                            .ToList()
                    };
                }

                if (column.Status == targetStatus)
                {
                    return column with
                    {
                        Issues = column.Issues
                            .Where(x => x.Id != issueId)
                            .Append(updatedIssue)
                            .OrderBy(x => x.BoardPosition)
                            .ToList()
                    };
                }

                return column;
            })
            .ToList();

        RenderMovedColumns(new[] { sourceStatus, targetStatus }, issueId, targetStatus);
    }

    private void ShowMoveToast(IssueStatus status)
    {
        _toastLabel.Text = $"Issue moved to {FormatStatus(status)}";
        PositionToast();
        _toastPanel.Visible = true;
        _toastPanel.BringToFront();
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

    private static string FormatStatus(IssueStatus status) => status switch
    {
        IssueStatus.InProgress => "In Progress",
        _ => status.ToString()
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toastTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
