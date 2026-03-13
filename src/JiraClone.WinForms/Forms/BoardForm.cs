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
    private readonly Label _sprintTitleLabel = JiraControlFactory.CreateLabel("No active sprint");
    private readonly Label _sprintDateLabel = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly Button _startSprintButton = JiraControlFactory.CreatePrimaryButton("Start Sprint");
    private readonly ComboBox _assigneeFilter = CreateFilterCombo(150);
    private readonly ComboBox _priorityFilter = CreateFilterCombo(130);
    private readonly ComboBox _typeFilter = CreateFilterCombo(130);
    private readonly TextBox _searchFilter = JiraControlFactory.CreateTextBox();
    private readonly Button _clearFiltersButton = JiraControlFactory.CreateSecondaryButton("Clear");
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

    public BoardForm(AppSession session)
    {
        _session = session;
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;

        _sprintTitleLabel.Font = JiraTheme.FontH2;
        _sprintTitleLabel.AutoSize = true;
        _sprintDateLabel.Font = JiraTheme.FontCaption;
        _sprintDateLabel.AutoSize = true;

        _startSprintButton.AutoSize = false;
        _startSprintButton.Size = new Size(136, 40);
        _startSprintButton.Click += async (_, _) => await StartSprintAsync();

        _searchFilter.Width = 240;
        _searchFilter.PlaceholderText = "Search issues";
        _clearFiltersButton.AutoSize = false;
        _clearFiltersButton.Size = new Size(88, 36);
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

        _boardScrollPanel.Controls.Add(_boardColumnsPanel);

        Controls.Add(_boardScrollPanel);
        Controls.Add(BuildFilterBar());
        Controls.Add(BuildTopBar());

        Load += async (_, _) => await LoadBoardAsync();
        Resize += (_, _) => UpdateColumnHeights();
    }

    private Control BuildTopBar()
    {
        var topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 74,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(16, 12, 16, 12),
        };

        topBar.Paint += (_, e) =>
        {
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawLine(pen, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);
        };

        var right = new Panel
        {
            Dock = DockStyle.Right,
            Width = 160,
            BackColor = JiraTheme.BgSurface,
        };
        right.Controls.Add(_startSprintButton);
        _startSprintButton.Location = new Point(8, 8);

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
        var filterBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = JiraTheme.BgPage,
            Padding = new Padding(16, 10, 16, 10),
            WrapContents = false,
        };

        filterBar.Controls.Add(_assigneeFilter);
        filterBar.Controls.Add(_priorityFilter);
        filterBar.Controls.Add(_typeFilter);
        filterBar.Controls.Add(_searchFilter);
        filterBar.Controls.Add(_clearFiltersButton);

        return filterBar;
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
                columns = await _session.Board.GetBoardAsync(project.Id, activeSprint?.Id);
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
            _sprintTitleLabel.Text = "No active sprint";
            _sprintDateLabel.Text = "Start a planned sprint to focus the board";
            _startSprintButton.Enabled = true;
            _startSprintButton.Text = "Start Sprint";
            return;
        }

        _sprintTitleLabel.Text = _activeSprint.Name;
        _sprintDateLabel.Text = FormatSprintDateRange(_activeSprint);
        _startSprintButton.Enabled = false;
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
        var assignee = _assigneeFilter.SelectedItem as string;
        var priority = _priorityFilter.SelectedItem as string;
        var type = _typeFilter.SelectedItem as string;
        var search = _searchFilter.Text.Trim();

        var filtered = _loadedColumns
            .Select(column => new BoardColumnDto(
                column.Status,
                column.Name,
                column.Issues
                    .Where(issue =>
                        (string.IsNullOrWhiteSpace(assignee) || assignee.StartsWith("All ") || issue.AssigneeNames.Contains(assignee)) &&
                        (string.IsNullOrWhiteSpace(priority) || priority.StartsWith("All ") || string.Equals(issue.Priority.ToString(), priority, StringComparison.OrdinalIgnoreCase)) &&
                        (string.IsNullOrWhiteSpace(type) || type.StartsWith("All ") || string.Equals(issue.Type.ToString(), type, StringComparison.OrdinalIgnoreCase)) &&
                        (string.IsNullOrWhiteSpace(search) ||
                         issue.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                         issue.IssueKey.Contains(search, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(issue => issue.BoardPosition)
                    .ToList()))
            .ToList();

        RenderColumns(filtered);
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
        _boardColumnsPanel.Controls.Clear();
        var totalWidth = 0;

        foreach (var column in columns)
        {
            var control = new BoardColumnControl(column)
            {
                Margin = new Padding(0, 0, 12, 0),
                Height = Math.Max(200, _boardScrollPanel.ClientSize.Height - 8),
            };
            control.IssueSelected += async (_, issueId) => await OpenIssueDetailsAsync(issueId);
            control.CreateIssueRequested += async (_, _) => await CreateIssueAsync();
            _boardColumnsPanel.Controls.Add(control);
            totalWidth += control.Width + control.Margin.Horizontal;
        }

        _boardColumnsPanel.ResumeLayout();
        _boardScrollPanel.AutoScrollMinSize = new Size(Math.Max(_boardScrollPanel.ClientSize.Width, totalWidth + 16), _boardScrollPanel.ClientSize.Height - 8);
    }

    private void UpdateColumnHeights()
    {
        foreach (Control control in _boardColumnsPanel.Controls)
        {
            control.Height = Math.Max(200, _boardScrollPanel.ClientSize.Height - 8);
        }
    }

    private async Task StartSprintAsync()
    {
        try
        {
            if (_projectId == 0)
            {
                return;
            }

            if (_activeSprint is not null)
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

    private async Task CreateIssueAsync()
    {
        using var dialog = new IssueEditorForm(_session, _projectId, null);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            await LoadBoardAsync();
        }
    }

    private async Task OpenIssueDetailsAsync(int issueId)
    {
        using var dialog = new IssueDetailsForm(_session, issueId, _projectId);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            await LoadBoardAsync();
        }
    }
}
