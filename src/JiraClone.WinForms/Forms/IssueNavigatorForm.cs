using JiraClone.Application.Jql;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Controls;
using JiraClone.WinForms.Helpers;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public sealed class IssueNavigatorForm : UserControl
{
    private static readonly string[] SupportedFields = ["project", "status", "type", "priority", "assignee", "reporter", "sprint", "created", "updated", "duedate", "storyPoints", "label", "component"];
    private static readonly string[] SupportedOperators = ["=", "!=", "in", ">=", "<=", ">", "<", "AND", "OR", "ORDER BY", "ASC", "DESC"];

    private readonly AppSession _session;
    private readonly Label _titleLabel = JiraControlFactory.CreateLabel("Issue");
    private readonly Label _subtitleLabel = JiraControlFactory.CreateLabel("Duyệt issue với tìm kiếm JQL nâng cao.", true);
    private readonly Label _countBadge = JiraControlFactory.CreateLabel("0 issue", true);
    private readonly JqlEditorControl _jqlEditor = new() { Dock = DockStyle.Top };
    private readonly ComboBox _statusFilter = CreateFilterCombo(170);
    private readonly ComboBox _priorityFilter = CreateFilterCombo(160);
    private readonly ComboBox _typeFilter = CreateFilterCombo(150);
    private readonly TextBox _searchBox = JiraControlFactory.CreateTextBox();
    private readonly Button _createIssueButton = JiraControlFactory.CreatePrimaryButton("Tạo issue");
    private readonly Button _runQueryButton = JiraControlFactory.CreatePrimaryButton("Chạy truy vấn");
    private readonly Button _clearQueryButton = JiraControlFactory.CreateSecondaryButton("Xóa");
    private readonly Label _queryHint = JiraControlFactory.CreateLabel("Ví dụ: assignee = currentUser() AND priority in (High, Highest) | type = Bug AND created >= -7d", true);
    private readonly Label _queryErrorLabel = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly Button _saveFilterButton = JiraControlFactory.CreateSecondaryButton("Lưu bộ lọc");
    private readonly Button _deleteFilterButton = JiraControlFactory.CreateSecondaryButton("Xóa bộ lọc");
    private readonly ListBox _savedFilters = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = JiraTheme.FontBody, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary };
    private readonly Label _savedFiltersEmpty = JiraControlFactory.CreateLabel("Chưa có bộ lọc đã lưu.", true);
    private readonly DataGridView _grid = new();
    private readonly Label _emptyState = JiraControlFactory.CreateLabel("No issues match the current query.", true);
    private readonly Button _openButton = JiraControlFactory.CreateSecondaryButton("Mở issue");
    private readonly JqlLexer _lexer = new();

    private int _projectId;
    private IReadOnlyList<IssueDto> _issues = Array.Empty<IssueDto>();
    private IReadOnlyList<SavedFilterDto> _savedFilterModels = Array.Empty<SavedFilterDto>();
    private IReadOnlyList<Sprint> _sprints = Array.Empty<Sprint>();
    private Project? _project;
    private string _shellSearch = string.Empty;
    private bool _loading;

    public IssueNavigatorForm(AppSession session)
    {
        _session = session;
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;

        _titleLabel.Font = JiraTheme.FontH1;
        _countBadge.AutoSize = true;
        _countBadge.BackColor = JiraTheme.Blue100;
        _countBadge.ForeColor = JiraTheme.PrimaryActive;
        _countBadge.Padding = new Padding(10, 6, 10, 6);
        _searchBox.Width = 280;
        _searchBox.PlaceholderText = "Tìm kiếm issue";
        _searchBox.Margin = new Padding(0, 0, 12, 0);
        _statusFilter.Margin = new Padding(0, 0, 12, 0);
        _priorityFilter.Margin = new Padding(0, 0, 12, 0);
        _typeFilter.Margin = new Padding(0, 0, 12, 0);
        _createIssueButton.AutoSize = false;
        _createIssueButton.Size = new Size(128, 36);
        _createIssueButton.Margin = new Padding(0);
        _savedFiltersEmpty.Dock = DockStyle.Fill;
        _savedFiltersEmpty.TextAlign = ContentAlignment.MiddleCenter;
        _savedFiltersEmpty.ForeColor = JiraTheme.TextSecondary;
        _savedFiltersEmpty.Visible = false;


        _runQueryButton.AutoSize = false;
        _runQueryButton.Size = new Size(120, 36);
        _clearQueryButton.AutoSize = false;
        _clearQueryButton.Size = new Size(100, 36);
        _saveFilterButton.AutoSize = false;
        _saveFilterButton.Size = new Size(108, 34);
        _deleteFilterButton.AutoSize = false;
        _deleteFilterButton.Size = new Size(112, 34);
        _openButton.AutoSize = false;
        _openButton.Size = new Size(116, 36);
        _openButton.Enabled = false;

        _queryHint.ForeColor = JiraTheme.TextSecondary;
        _queryErrorLabel.ForeColor = JiraTheme.Danger;
        _queryErrorLabel.Visible = false;
        _jqlEditor.SuggestionProvider = GetSuggestions;

        _jqlEditor.QueryChanged += (_, _) => ValidateQuery();
        _jqlEditor.Editor.KeyDown += async (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await ExecuteCurrentQueryAsync();
            }
        };
        _runQueryButton.Click += async (_, _) => await ExecuteCurrentQueryAsync();
        _clearQueryButton.Click += async (_, _) => await ClearQueryAsync();
        _saveFilterButton.Click += async (_, _) => await SaveCurrentFilterAsync();
        _deleteFilterButton.Click += async (_, _) => await DeleteSelectedFilterAsync();
        _savedFilters.SelectedIndexChanged += (_, _) => UpdateSavedFilterButtons();
        _savedFilters.DoubleClick += async (_, _) => await ApplySelectedFilterAsync();
        _openButton.Click += async (_, _) => await OpenSelectedIssueAsync();
        _searchBox.TextChanged += (_, _) => SetShellSearch(_searchBox.Text);
        _statusFilter.SelectedIndexChanged += (_, _) => BindIssues();
        _priorityFilter.SelectedIndexChanged += (_, _) => BindIssues();
        _typeFilter.SelectedIndexChanged += (_, _) => BindIssues();
        _createIssueButton.Click += async (_, _) => await CreateIssueAsync();

        ConfigureGrid();
        Controls.Add(BuildLayout());
        Load += async (_, _) => await RefreshIssuesAsync();
    }

    public async Task RefreshIssuesAsync(CancellationToken cancellationToken = default)
    {
        if (_loading)
        {
            return;
        }

        try
        {
            _loading = true;
            Project? project = null;
            IReadOnlyList<Sprint> sprints = Array.Empty<Sprint>();
            IReadOnlyList<SavedFilterDto> savedFilters = Array.Empty<SavedFilterDto>();
            var currentUserId = _session.CurrentUserContext.RequireUserId();

            await _session.RunSerializedAsync(async () =>
            {
                project = await _session.Projects.GetActiveProjectAsync(cancellationToken);
                if (project is null)
                {
                    return;
                }

                sprints = await _session.Sprints.GetByProjectAsync(project.Id, cancellationToken);
                savedFilters = await _session.SavedFilters.GetByProjectAsync(project.Id, currentUserId, cancellationToken);
            }, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            _project = project;
            _sprints = sprints;
            _savedFilterModels = savedFilters;
            _savedFilters.DataSource = savedFilters.Select(filter => new SavedFilterListItem(filter)).ToList();
            _savedFilters.DisplayMember = nameof(SavedFilterListItem.DisplayText);
            _savedFilters.ValueMember = nameof(SavedFilterListItem.Id);
            UpdateSavedFilterButtons();

            if (project is null)
            {
                _projectId = 0;
                _issues = Array.Empty<IssueDto>();
                BindIssues();
                return;
            }

            _projectId = project.Id;
            _subtitleLabel.Text = $"Duyệt issue trong {project.Name} với tìm kiếm JQL nâng cao.";
            await ExecuteCurrentQueryAsync(cancellationToken);
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
            _loading = false;
        }
    }

    public void SetShellSearch(string value)
    {
        var target = value?.Trim() ?? string.Empty;
        if (string.Equals(_shellSearch, target, StringComparison.Ordinal) && string.Equals(_searchBox.Text, target, StringComparison.Ordinal))
        {
            return;
        }

        _shellSearch = target;
        if (!string.Equals(_searchBox.Text, target, StringComparison.Ordinal))
        {
            _searchBox.Text = target;
        }

        BindIssues();
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = JiraTheme.BgPage,
            Padding = new Padding(20, 18, 20, 20)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildToolbar(), 0, 1);
        root.Controls.Add(BuildQueryRegion(), 0, 2);
        root.Controls.Add(BuildContentRegion(), 0, 3);
        return root;
    }

    private Control BuildToolbar()
    {
        var host = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = JiraTheme.BgPage, Padding = new Padding(0, 0, 0, 12) };
        var surface = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface, Padding = new Padding(14, 10, 14, 10) };
        surface.Paint += (_, e) =>
        {
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, surface.Width - 1, surface.Height - 1);
        };

        var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = JiraTheme.BgSurface, Margin = new Padding(0), Padding = new Padding(0) };
        filters.Controls.Add(_statusFilter);
        filters.Controls.Add(_priorityFilter);
        filters.Controls.Add(_typeFilter);
        filters.Controls.Add(_searchBox);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 260, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = JiraTheme.BgSurface, Margin = new Padding(0), Padding = new Padding(0) };
        actions.Controls.Add(_createIssueButton);
        actions.Controls.Add(_openButton);
        _openButton.Margin = new Padding(0, 0, 10, 0);

        surface.Controls.Add(actions);
        surface.Controls.Add(filters);
        host.Controls.Add(surface);
        return host;
    }

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = JiraTheme.BgPage };

        var meta = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = JiraTheme.BgPage,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _countBadge.Margin = new Padding(12, 6, 0, 0);
        meta.Controls.Add(_titleLabel);
        meta.Controls.Add(_countBadge);

        header.Controls.Add(_subtitleLabel);
        header.Controls.Add(meta);
        meta.Location = new Point(0, 0);
        _subtitleLabel.Location = new Point(0, 42);
        return header;
    }

    private Control BuildQueryRegion()
    {
        var host = new Panel { Dock = DockStyle.Top, Height = 164, BackColor = JiraTheme.BgPage, Padding = new Padding(0, 0, 0, 16) };
        var surface = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface, Padding = new Padding(14, 12, 14, 12) };
        surface.Paint += (_, e) =>
        {
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, surface.Width - 1, surface.Height - 1);
        };

        var actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 436, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = JiraTheme.BgSurface };
        actions.Controls.Add(_runQueryButton);
        actions.Controls.Add(_clearQueryButton);
        _runQueryButton.Margin = new Padding(0, 24, 10, 0);
        _clearQueryButton.Margin = new Padding(0, 24, 10, 0);

        _queryHint.Dock = DockStyle.Bottom;
        _queryHint.Height = 22;
        _queryErrorLabel.Dock = DockStyle.Bottom;
        _queryErrorLabel.Height = 24;

        surface.Controls.Add(actions);
        surface.Controls.Add(_jqlEditor);
        surface.Controls.Add(_queryErrorLabel);
        surface.Controls.Add(_queryHint);
        host.Controls.Add(surface);
        return host;
    }

    private Control BuildContentRegion()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            BackColor = JiraTheme.Border,
            SplitterWidth = 6
        };
        split.HandleCreated += (_, _) => SplitContainerHelper.ConfigureSafeLayout(split, 240, 220, 480);
        split.SizeChanged += (_, _) => SplitContainerHelper.ConfigureSafeLayout(split, 240, 220, 480);
        split.Panel1.Controls.Add(BuildFiltersSidebar());
        split.Panel2.Controls.Add(BuildResultsSurface());
        return split;
    }

    private Control BuildFiltersSidebar()
    {
        var surface = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface, Padding = new Padding(14, 12, 14, 12) };
        surface.Paint += (_, e) =>
        {
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, surface.Width - 1, surface.Height - 1);
        };

        var title = JiraControlFactory.CreateLabel("Bộ lọc");
        title.Font = JiraTheme.FontColumnHeader;
        title.Dock = DockStyle.Top;
        title.Height = 28;

        var caption = JiraControlFactory.CreateLabel("Các truy vấn JQL đã lưu cho dự án này.", true);
        caption.Dock = DockStyle.Top;
        caption.Height = 20;
        caption.ForeColor = JiraTheme.TextSecondary;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = JiraTheme.BgSurface, Margin = new Padding(0), Padding = new Padding(0, 8, 0, 0) };
        buttons.Controls.Add(_saveFilterButton);
        buttons.Controls.Add(_deleteFilterButton);
        _saveFilterButton.Margin = new Padding(0, 0, 8, 0);
        _deleteFilterButton.Margin = new Padding(0);

        surface.Controls.Add(_savedFiltersEmpty);
        surface.Controls.Add(_savedFilters);
        surface.Controls.Add(buttons);
        surface.Controls.Add(caption);
        surface.Controls.Add(title);
        return surface;
    }

    private Control BuildResultsSurface()
    {
        var surface = new DoubleBufferedPanel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
        surface.Paint += (_, e) =>
        {
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, surface.Width - 1, surface.Height - 1);
        };

        _emptyState.Dock = DockStyle.Fill;
        _emptyState.TextAlign = ContentAlignment.MiddleCenter;
        _emptyState.Visible = false;

        surface.Controls.Add(_emptyState);
        surface.Controls.Add(_grid);
        return surface;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.MultiSelect = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoGenerateColumns = false;
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _grid.RowTemplate.Height = 36;
        JiraTheme.StyleDataGridView(_grid);
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.ColumnHeadersHeight = 42;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.SelectionChanged += (_, _) => _openButton.Enabled = _grid.CurrentRow?.DataBoundItem is IssueRow;
        _grid.CellDoubleClick += async (_, eventArgs) =>
        {
            if (eventArgs.RowIndex >= 0)
            {
                await OpenSelectedIssueAsync();
            }
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(IssueRow.Key), HeaderText = "Key", Width = 110, MinimumWidth = 100 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(IssueRow.Summary), HeaderText = "Summary", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 260 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(IssueRow.Status), HeaderText = "Status", Width = 130, MinimumWidth = 120 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(IssueRow.Priority), HeaderText = "Priority", Width = 120, MinimumWidth = 110 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(IssueRow.Type), HeaderText = "Type", Width = 110, MinimumWidth = 100 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(IssueRow.Assignees), HeaderText = "Assignees", Width = 220, MinimumWidth = 180 });
    }

    private void ValidateQuery()
    {
        var query = _jqlEditor.QueryText.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            _queryErrorLabel.Visible = false;
            _queryErrorLabel.Text = string.Empty;
            return;
        }

        try
        {
            _session.Jql.Parse(query);
            _queryErrorLabel.Visible = false;
            _queryErrorLabel.Text = string.Empty;
        }
        catch (JqlParseException ex)
        {
            _queryErrorLabel.Visible = true;
            _queryErrorLabel.Text = $"Lỗi phân tích tại vị trí {ex.Position + 1}: {ex.Message}";
        }
        catch (Exception ex)
        {
            _queryErrorLabel.Visible = true;
            _queryErrorLabel.Text = ex.Message;
        }
    }

    private async Task ExecuteCurrentQueryAsync(CancellationToken cancellationToken = default)
    {
        if (_projectId == 0)
        {
            return;
        }

        try
        {
            var query = _jqlEditor.QueryText.Trim();
            ValidateQuery();
            if (_queryErrorLabel.Visible)
            {
                return;
            }

            _issues = await _session.Jql.ExecuteQueryAsync(query, _projectId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            PopulateQuickFilters();
            BindIssues();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private void BindIssues()
    {
        var search = _searchBox.Text.Trim();
        var status = _statusFilter.SelectedIndex <= 0 ? null : _statusFilter.SelectedItem as string;
        var priority = _priorityFilter.SelectedIndex <= 0 ? null : _priorityFilter.SelectedItem as string;
        var type = _typeFilter.SelectedIndex <= 0 ? null : _typeFilter.SelectedItem as string;

        var filtered = _issues
            .Where(issue => string.IsNullOrWhiteSpace(search)
                || issue.IssueKey.Contains(search, StringComparison.OrdinalIgnoreCase)
                || issue.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                || issue.ReporterName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || issue.AssigneeNames.Any(name => name.Contains(search, StringComparison.OrdinalIgnoreCase))
                || issue.Labels.Any(label => label.Contains(search, StringComparison.OrdinalIgnoreCase))
                || issue.Components.Any(component => component.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .Where(issue => string.IsNullOrWhiteSpace(status) || string.Equals(issue.WorkflowStatusName, status, StringComparison.OrdinalIgnoreCase))
            .Where(issue => string.IsNullOrWhiteSpace(priority) || string.Equals(FormatPriority(issue.Priority), priority, StringComparison.OrdinalIgnoreCase))
            .Where(issue => string.IsNullOrWhiteSpace(type) || string.Equals(FormatType(issue.Type), type, StringComparison.OrdinalIgnoreCase))
            .Select(issue => new IssueRow(
                issue.Id,
                issue.IssueKey,
                issue.Title,
                issue.WorkflowStatusName,
                FormatPriority(issue.Priority),
                FormatType(issue.Type),
                issue.AssigneeNames.Count == 0 ? "Chưa giao" : string.Join(", ", issue.AssigneeNames)))
            .ToList();

        _grid.DataSource = filtered;
        _savedFiltersEmpty.Visible = _savedFilterModels.Count == 0;
        _savedFilters.Visible = _savedFilterModels.Count > 0;
        _emptyState.Visible = filtered.Count == 0;
        _grid.Visible = filtered.Count > 0;
        _countBadge.Text = filtered.Count == 1 ? "1 issue" : $"{filtered.Count} issue";
        _openButton.Enabled = filtered.Count > 0 && _grid.CurrentRow?.DataBoundItem is IssueRow;
    }

    private void PopulateQuickFilters()
    {
        ResetFilter(_statusFilter, "Tất cả trạng thái", _issues.Select(issue => issue.WorkflowStatusName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x));
        ResetFilter(_priorityFilter, "Tất cả mức ưu tiên", _issues.Select(issue => FormatPriority(issue.Priority)).Distinct(StringComparer.OrdinalIgnoreCase));
        ResetFilter(_typeFilter, "Tất cả loại", _issues.Select(issue => FormatType(issue.Type)).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static void ResetFilter(ComboBox comboBox, string allLabel, IEnumerable<string> values)
    {
        var selected = comboBox.SelectedItem as string;
        comboBox.Items.Clear();
        comboBox.Items.Add(allLabel);
        foreach (var value in values)
        {
            comboBox.Items.Add(value);
        }

        comboBox.SelectedItem = selected is not null && comboBox.Items.Contains(selected) ? selected : allLabel;
    }

    private async Task ClearQueryAsync()
    {
        _jqlEditor.QueryText = string.Empty;
        _queryErrorLabel.Visible = false;
        _queryErrorLabel.Text = string.Empty;
        await ExecuteCurrentQueryAsync();
    }

    private async Task SaveCurrentFilterAsync()
    {
        if (_projectId == 0)
        {
            return;
        }

        var query = _jqlEditor.QueryText.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            ErrorDialogService.Show("Hãy nhập truy vấn JQL trước khi lưu bộ lọc.");
            return;
        }

        using var dialog = new SaveFilterDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            await _session.SavedFilters.CreateAsync(_projectId, _session.CurrentUserContext.RequireUserId(), dialog.FilterName, query);
            await RefreshIssuesAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task DeleteSelectedFilterAsync()
    {
        if (_savedFilters.SelectedItem is not SavedFilterListItem selected)
        {
            return;
        }

        if (MessageBox.Show(this, $"Delete saved filter '{selected.DisplayText}'?", "Delete Filter", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            await _session.SavedFilters.DeleteAsync(selected.Id, _session.CurrentUserContext.RequireUserId());
            await RefreshIssuesAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task ApplySelectedFilterAsync()
    {
        if (_savedFilters.SelectedItem is not SavedFilterListItem selected)
        {
            return;
        }

        _jqlEditor.QueryText = selected.QueryText;
        await ExecuteCurrentQueryAsync();
    }

    private void UpdateSavedFilterButtons()
    {
        _deleteFilterButton.Enabled = _savedFilters.SelectedItem is SavedFilterListItem;
    }

    private IReadOnlyList<string> GetSuggestions(string text, int caretPosition)
    {
        var prefix = caretPosition <= 0 ? string.Empty : text[..Math.Min(caretPosition, text.Length)];
        var fragment = GetCurrentFragment(prefix);
        var available = ResolveSuggestionPool(prefix);
        return available
            .Where(item => string.IsNullOrWhiteSpace(fragment) || item.StartsWith(fragment, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Length)
            .ThenBy(item => item)
            .ToList();
    }

    private IReadOnlyList<string> ResolveSuggestionPool(string prefix)
    {
        IReadOnlyList<JqlToken> tokens;
        try
        {
            tokens = _lexer.Tokenize(prefix);
        }
        catch
        {
            return SupportedFields;
        }

        var effective = tokens.Where(token => token.Kind != JqlTokenKind.EndOfInput).ToList();
        if (effective.Count == 0)
        {
            return SupportedFields;
        }

        if (EndsWithOrderBy(effective))
        {
            return SupportedFields;
        }

        var last = effective[^1];
        if (last.Kind is JqlTokenKind.And or JqlTokenKind.Or or JqlTokenKind.Comma)
        {
            return IsInsideInList(effective) ? GetValueSuggestions(ResolveCurrentField(effective)) : SupportedFields;
        }

        if (last.Kind == JqlTokenKind.Identifier && IsExpectingOperator(effective))
        {
            return SupportedOperators;
        }

        if (last.Kind is JqlTokenKind.Equals or JqlTokenKind.NotEquals or JqlTokenKind.GreaterThan or JqlTokenKind.GreaterThanOrEqual or JqlTokenKind.LessThan or JqlTokenKind.LessThanOrEqual or JqlTokenKind.In or JqlTokenKind.OpenParen)
        {
            return GetValueSuggestions(ResolveCurrentField(effective));
        }

        return SupportedFields.Concat(GetValueSuggestions(ResolveCurrentField(effective))).Concat(SupportedOperators).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private bool EndsWithOrderBy(IReadOnlyList<JqlToken> tokens)
    {
        return tokens.Count >= 2 && tokens[^2].Kind == JqlTokenKind.Order && tokens[^1].Kind == JqlTokenKind.By;
    }

    private bool IsExpectingOperator(IReadOnlyList<JqlToken> tokens)
    {
        if (tokens.Count == 0 || tokens[^1].Kind != JqlTokenKind.Identifier)
        {
            return false;
        }

        if (tokens.Count == 1)
        {
            return true;
        }

        var previous = tokens[^2].Kind;
        return previous is JqlTokenKind.And or JqlTokenKind.Or or JqlTokenKind.By or JqlTokenKind.Comma or JqlTokenKind.OpenParen;
    }

    private bool IsInsideInList(IReadOnlyList<JqlToken> tokens)
    {
        return tokens.Count >= 2 && (tokens[^2].Kind == JqlTokenKind.In || tokens[^2].Kind == JqlTokenKind.OpenParen);
    }

    private string? ResolveCurrentField(IReadOnlyList<JqlToken> tokens)
    {
        for (var index = tokens.Count - 1; index >= 0; index--)
        {
            var token = tokens[index];
            if (token.Kind != JqlTokenKind.Identifier)
            {
                continue;
            }

            var next = index + 1 < tokens.Count ? tokens[index + 1].Kind : JqlTokenKind.EndOfInput;
            if (next is JqlTokenKind.Equals or JqlTokenKind.NotEquals or JqlTokenKind.GreaterThan or JqlTokenKind.GreaterThanOrEqual or JqlTokenKind.LessThan or JqlTokenKind.LessThanOrEqual or JqlTokenKind.In or JqlTokenKind.Not)
            {
                return token.Text;
            }
        }

        return tokens.LastOrDefault(token => token.Kind == JqlTokenKind.Identifier)?.Text;
    }

    private IReadOnlyList<string> GetValueSuggestions(string? field)
    {
        var project = _project;
        var currentUser = _session.CurrentUserContext.CurrentUser;
        var normalizedField = (field ?? string.Empty).Replace("_", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();
        return normalizedField switch
        {
            "project" when project is not null => [project.Key, Quote(project.Name)],
            "status" when project is not null => project.WorkflowDefinitions.SelectMany(workflow => workflow.Statuses).OrderBy(status => status.DisplayOrder).Select(status => Quote(status.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            "type" => Enum.GetNames<IssueType>(),
            "priority" => Enum.GetNames<IssuePriority>(),
            "assignee" or "reporter" when project is not null => project.Members.Select(member => Quote(member.User.DisplayName)).Distinct(StringComparer.OrdinalIgnoreCase).Append("currentUser()").ToList(),
            "sprint" => _sprints.Select(sprint => Quote(sprint.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            "created" or "updated" or "duedate" => ["-7d", "-14d", DateTime.Today.ToString("yyyy-MM-dd")],
            "storypoints" => ["1", "2", "3", "5", "8", "13"],
            "label" when project is not null => project.Labels.Select(label => Quote(label.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            "component" when project is not null => project.Components.Select(component => Quote(component.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            _ => SupportedFields
        };
    }

    private static string GetCurrentFragment(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return string.Empty;
        }

        var index = prefix.Length - 1;
        while (index >= 0 && (char.IsLetterOrDigit(prefix[index]) || prefix[index] is '_' or '-' or '.'))
        {
            index--;
        }

        return prefix[(index + 1)..];
    }

    private static string Quote(string value) => value.Contains(' ') ? $"\"{value}\"" : value;

    private async Task CreateIssueAsync()
    {
        try
        {
            if (_projectId == 0)
            {
                return;
            }

            using var dialog = new IssueEditorForm(_session, _projectId, null);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                await RefreshIssuesAsync();
            }
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
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

    private async Task OpenSelectedIssueAsync()
    {
        try
        {
            if (_grid.CurrentRow?.DataBoundItem is not IssueRow issue || _projectId == 0)
            {
                return;
            }

            using var dialog = new IssueDetailsForm(_session, issue.Id, _projectId);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                await RefreshIssuesAsync();
            }
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private static string FormatPriority(IssuePriority priority) => priority switch
    {
        IssuePriority.Highest => "Highest",
        _ => priority.ToString()
    };

    private static string FormatType(IssueType type) => type switch
    {
        IssueType.Task => "Task",
        IssueType.Bug => "Bug",
        IssueType.Story => "Story",
        IssueType.Epic => "Epic",
        IssueType.Subtask => "Subtask",
        _ => type.ToString()
    };

    private sealed record IssueRow(int Id, string Key, string Summary, string Status, string Priority, string Type, string Assignees);

    private sealed record SavedFilterListItem(SavedFilterDto Filter)
    {
        public int Id => Filter.Id;
        public string QueryText => Filter.QueryText;
        public string DisplayText => Filter.IsFavorite ? $"* {Filter.Name}" : Filter.Name;
        public override string ToString() => DisplayText;
    }

    private sealed class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel() => DoubleBuffered = true;
    }

    private sealed class SaveFilterDialog : Form
    {
        private readonly TextBox _name = JiraControlFactory.CreateTextBox();

        public SaveFilterDialog()
        {
            Text = "Lưu bộ lọc";
            AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
            Width = 360;
            Height = 190;
            MinimumSize = new Size(360, 190);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = JiraTheme.BgSurface;
            Font = JiraTheme.FontBody;

            var save = JiraControlFactory.CreatePrimaryButton("Save");
            var cancel = JiraControlFactory.CreateSecondaryButton("Cancel");
            save.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(_name.Text))
                {
                    ErrorDialogService.Show("Tên bộ lọc là bắt buộc.");
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            };
            cancel.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = JiraTheme.BgSurface };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);

            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16), BackColor = JiraTheme.BgSurface };
            layout.Controls.Add(JiraControlFactory.CreateLabel("Tên bộ lọc", true));
            layout.Controls.Add(_name);
            Controls.Add(layout);
            Controls.Add(buttons);
        }

        public string FilterName => _name.Text.Trim();
    }
}







