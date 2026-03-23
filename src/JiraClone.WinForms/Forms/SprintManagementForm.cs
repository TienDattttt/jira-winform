using JiraClone.Application.Roles;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Dialogs;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public class SprintManagementForm : UserControl
{
    private readonly AppSession _session;
    private readonly ListView _listView = new()
    {
        Dock = DockStyle.Fill,
        FullRowSelect = true,
        MultiSelect = false,
        View = View.Details,
        BorderStyle = BorderStyle.None,
        BackColor = JiraTheme.BgSurface,
        ForeColor = JiraTheme.TextPrimary,
        Font = JiraTheme.FontBody,
        HideSelection = false,
    };
    private readonly Button _createButton = JiraControlFactory.CreatePrimaryButton("Create Sprint");
    private readonly Button _assignButton = JiraControlFactory.CreateSecondaryButton("Assign Issues");
    private readonly Button _startButton = JiraControlFactory.CreateSecondaryButton("Start Sprint");
    private readonly Button _closeButton = JiraControlFactory.CreateSecondaryButton("Close Sprint");
    private readonly Label _helpLabel = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly Label _plannedBadge = CreateBadgeLabel();
    private readonly Label _activeBadge = CreateBadgeLabel();
    private readonly Label _closedBadge = CreateBadgeLabel();
    private readonly Label _emptyState = JiraControlFactory.CreateLabel("No sprints yet. Create the first sprint to start planning work.", true);
    private List<Sprint> _sprints = [];
    private int _projectId;
    private bool _isLoading;
    private string _shellSearch = string.Empty;

    public SprintManagementForm(AppSession session)
    {
        _session = session;
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;

        JiraTheme.StyleListView(_listView);
        _listView.Columns.Add("Sprint", 240);
        _listView.Columns.Add("State", 120);
        _listView.Columns.Add("Start", 140);
        _listView.Columns.Add("End", 140);
        _listView.Columns.Add("Goal", 520);
        _listView.SelectedIndexChanged += (_, _) => UpdateActionState();
        _listView.DoubleClick += async (_, _) => await AssignIssuesAsync();
        _listView.Resize += (_, _) => ApplyResponsiveColumns();

        _createButton.Click += async (_, _) => await CreateSprintAsync();
        _assignButton.Click += async (_, _) => await AssignIssuesAsync();
        _startButton.Click += async (_, _) => await StartSprintAsync();
        _closeButton.Click += async (_, _) => await CloseSprintAsync();
        ConfigureActionButton(_createButton, 164);
        ConfigureActionButton(_assignButton, 156);
        ConfigureActionButton(_startButton, 156);
        ConfigureActionButton(_closeButton, 148);

        _helpLabel.ForeColor = JiraTheme.TextSecondary;
        _emptyState.Dock = DockStyle.Fill;
        _emptyState.TextAlign = ContentAlignment.MiddleCenter;
        _emptyState.Visible = false;

        Controls.Add(BuildSurface());
        Controls.Add(BuildActions());
        Controls.Add(BuildHeader());

        Load += async (_, _) => await LoadSprintsAsync();
        UpdateActionState();
    }

    private Sprint? SelectedSprint => _listView.SelectedItems.Count == 0 ? null : _listView.SelectedItems[0].Tag as Sprint;

    public Task RefreshSprintsAsync(CancellationToken cancellationToken = default) => LoadSprintsAsync(cancellationToken);

    public void SetShellSearch(string value)
    {
        _shellSearch = value?.Trim() ?? string.Empty;
        BindSprints();
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 116,
            BackColor = JiraTheme.BgPage,
            Padding = new Padding(20, 18, 20, 8),
        };

        var title = JiraControlFactory.CreateLabel("Sprints");
        title.Font = JiraTheme.FontH1;
        title.Location = new Point(0, 0);

        var subtitle = JiraControlFactory.CreateLabel("Plan, start, and close sprints with the same flow users expect from Jira.", true);
        subtitle.Location = new Point(0, 40);

        var badges = new FlowLayoutPanel
        {
            Location = new Point(0, 70),
            AutoSize = true,
            WrapContents = false,
            BackColor = JiraTheme.BgPage,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        badges.Controls.AddRange([_plannedBadge, _activeBadge, _closedBadge]);

        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(badges);
        return header;
    }

    private Control BuildActions()
    {
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 68,
            Padding = new Padding(16, 8, 16, 12),
            BackColor = JiraTheme.BgPage,
            WrapContents = false,
        };
        actions.Controls.AddRange([_createButton, _assignButton, _startButton, _closeButton]);
        return actions;
    }

    private Control BuildSurface()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 0, 20, 20),
            BackColor = JiraTheme.BgPage,
        };

        var surface = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(0),
        };
        surface.Paint += (_, e) =>
        {
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, surface.Width - 1, surface.Height - 1);
        };

        var topMeta = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(16, 16, 16, 8),
        };
        _helpLabel.Location = new Point(0, 0);
        topMeta.Controls.Add(_helpLabel);

        surface.Controls.Add(_emptyState);
        surface.Controls.Add(_listView);
        surface.Controls.Add(topMeta);
        host.Controls.Add(surface);
        return host;
    }

    private async Task LoadSprintsAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoading || !Visible)
        {
            return;
        }

        try
        {
            _isLoading = true;
            Project? project = null;
            List<Sprint> sprints = [];

            await _session.RunSerializedAsync(async () =>
            {
                project = await _session.Projects.GetActiveProjectAsync(cancellationToken);
                if (project is null)
                {
                    return;
                }

                sprints = (await _session.Sprints.GetByProjectAsync(project.Id, cancellationToken))
                    .OrderByDescending(x => x.State == SprintState.Active)
                    .ThenByDescending(x => x.StartDate)
                    .ThenByDescending(x => x.Id)
                    .ToList();
            }, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            if (project is null)
            {
                return;
            }

            _projectId = project.Id;
            _sprints = sprints;
            BindSprints();

            var activeSprint = _sprints.FirstOrDefault(x => x.State == SprintState.Active);
            _helpLabel.Text = activeSprint is null
                ? "No active sprint. Start a planned sprint to focus the board."
                : $"Active sprint: {activeSprint.Name} | {FormatSprintDateRange(activeSprint)}";
            _plannedBadge.Text = $"Planned {_sprints.Count(x => x.State == SprintState.Planned)}";
            _activeBadge.Text = $"Active {_sprints.Count(x => x.State == SprintState.Active)}";
            _closedBadge.Text = $"Closed {_sprints.Count(x => x.State == SprintState.Closed)}";
            UpdateActionState();
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
            _isLoading = false;
        }
    }

    private void BindSprints()
    {
        var filtered = _sprints
            .Where(x => string.IsNullOrWhiteSpace(_shellSearch)
                || x.Name.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)
                || (x.Goal ?? string.Empty).Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)
                || x.State.ToString().Contains(_shellSearch, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _listView.BeginUpdate();
        _listView.Items.Clear();
        foreach (var sprint in filtered)
        {
            var item = new ListViewItem(sprint.Name) { Tag = sprint };
            item.SubItems.Add(sprint.State.ToString());
            item.SubItems.Add(sprint.StartDate?.ToString("dd MMM yyyy") ?? "-");
            item.SubItems.Add(sprint.EndDate?.ToString("dd MMM yyyy") ?? "-");
            item.SubItems.Add(sprint.Goal ?? string.Empty);
            _listView.Items.Add(item);
        }
        _listView.EndUpdate();
        ApplyResponsiveColumns();

        _emptyState.Visible = filtered.Count == 0;
        _listView.Visible = filtered.Count > 0;
        UpdateActionState();
    }

    private void ApplyResponsiveColumns()
    {
        if (_listView.ClientSize.Width <= 0)
        {
            return;
        }

        var sprintWidth = 190;
        var stateWidth = 110;
        var startWidth = 120;
        var endWidth = 120;
        var goalWidth = Math.Max(240, _listView.ClientSize.Width - sprintWidth - stateWidth - startWidth - endWidth - 12);

        _listView.Columns[0].Width = sprintWidth;
        _listView.Columns[1].Width = stateWidth;
        _listView.Columns[2].Width = startWidth;
        _listView.Columns[3].Width = endWidth;
        _listView.Columns[4].Width = goalWidth;
    }

    private static string FormatSprintDateRange(Sprint sprint)
    {
        var start = sprint.StartDate?.ToString("dd MMM yyyy") ?? "?";
        var end = sprint.EndDate?.ToString("dd MMM yyyy") ?? "?";
        return $"{start} - {end}";
    }

    private static Label CreateBadgeLabel()
    {
        var label = JiraControlFactory.CreateLabel(string.Empty, true);
        label.AutoSize = true;
        label.BackColor = JiraTheme.Blue100;
        label.ForeColor = JiraTheme.PrimaryActive;
        label.Padding = new Padding(10, 6, 10, 6);
        label.Margin = new Padding(0, 0, 8, 0);
        return label;
    }

    private static void ConfigureActionButton(Button button, int width)
    {
        button.AutoSize = false;
        button.Width = width;
        button.Height = 40;
    }

    private void UpdateActionState()
    {
        var canManage = _session.Authorization.IsInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var selectedSprint = SelectedSprint;
        var hasProject = _projectId > 0;

        _createButton.Enabled = canManage && hasProject;
        _assignButton.Enabled = canManage && selectedSprint is not null && selectedSprint.State != SprintState.Closed;
        _startButton.Enabled = canManage && selectedSprint?.State == SprintState.Planned;
        _closeButton.Enabled = canManage && selectedSprint?.State == SprintState.Active;
    }

    private async Task CreateSprintAsync()
    {
        try
        {
            if (_projectId == 0)
            {
                return;
            }

            using var dialog = new CreateSprintDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            await _session.Sprints.CreateAsync(_projectId, dialog.SprintName, dialog.Goal, null, null);
            await LoadSprintsAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task AssignIssuesAsync()
    {
        try
        {
            var sprint = SelectedSprint;
            if (sprint is null)
            {
                ErrorDialogService.Show("Select a sprint first.");
                return;
            }

            using var dialog = new AssignToSprintDialog(sprint, await _session.Sprints.GetAssignableIssuesAsync(_projectId));
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            await _session.Sprints.AssignIssuesAsync(sprint.Id, dialog.SelectedIssueIds);
            await LoadSprintsAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task StartSprintAsync()
    {
        try
        {
            var sprint = SelectedSprint;
            if (sprint is null)
            {
                ErrorDialogService.Show("Select a sprint first.");
                return;
            }

            await _session.Sprints.StartSprintAsync(sprint.Id);
            await LoadSprintsAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task CloseSprintAsync()
    {
        try
        {
            var sprint = SelectedSprint;
            if (sprint is null)
            {
                ErrorDialogService.Show("Select a sprint first.");
                return;
            }

            var nextSprints = _sprints.Where(x => x.Id != sprint.Id && x.State != SprintState.Closed).ToList();
            using var dialog = new CloseSprintDialog(sprint, nextSprints);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            await _session.Sprints.CloseSprintAsync(sprint.Id, dialog.MoveIncompleteToSprintId);
            await LoadSprintsAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private sealed class CreateSprintDialog : Form
    {
        private readonly TextBox _name = JiraControlFactory.CreateTextBox();
        private readonly TextBox _goal = JiraControlFactory.CreateTextBox();

        public CreateSprintDialog()
        {
            Text = "Create Sprint";
            AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(640, 420);
            MinimumSize = new Size(640, 420);
            BackColor = JiraTheme.BgSurface;
            Font = JiraTheme.FontBody;

            _name.Dock = DockStyle.Fill;
            _name.MinimumSize = new Size(0, 42);
            _goal.Multiline = true;
            _goal.ScrollBars = ScrollBars.Vertical;
            _goal.AcceptsReturn = true;
            _goal.Dock = DockStyle.Fill;
            _goal.MinimumSize = new Size(0, 140);

            var save = JiraControlFactory.CreatePrimaryButton("Create sprint");
            var cancel = JiraControlFactory.CreateSecondaryButton("Cancel");
            save.AutoSize = false;
            save.Size = new Size(136, 42);
            cancel.AutoSize = false;
            cancel.Size = new Size(104, 42);
            save.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(_name.Text))
                {
                    MessageBox.Show(this, "Sprint name is required.", "Create Sprint", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            AcceptButton = save;
            CancelButton = cancel;

            var title = JiraControlFactory.CreateLabel("Create sprint");
            title.Font = JiraTheme.FontH2;
            title.Dock = DockStyle.Top;
            title.Height = 34;

            var subtitle = JiraControlFactory.CreateLabel("Add a sprint name and an optional goal to keep planning aligned with the board.", true);
            subtitle.Dock = DockStyle.Top;
            subtitle.AutoSize = false;
            subtitle.Height = 40;

            var nameLabel = JiraControlFactory.CreateLabel("Sprint name", true);
            nameLabel.Dock = DockStyle.Top;
            nameLabel.Height = 24;

            var nameHost = new Panel
            {
                Dock = DockStyle.Top,
                Height = 82,
                BackColor = JiraTheme.BgSurface,
            };
            nameHost.Controls.Add(_name);
            nameHost.Controls.Add(nameLabel);

            var goalLabel = JiraControlFactory.CreateLabel("Goal", true);
            goalLabel.Dock = DockStyle.Top;
            goalLabel.Height = 24;

            var goalHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = JiraTheme.BgSurface,
            };
            goalHost.Controls.Add(_goal);
            goalHost.Controls.Add(goalLabel);

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = JiraTheme.BgSurface,
                Padding = new Padding(28, 24, 28, 0),
            };
            content.Controls.Add(goalHost);
            content.Controls.Add(nameHost);
            content.Controls.Add(subtitle);
            content.Controls.Add(title);

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 76,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(24, 16, 24, 18),
                BackColor = JiraTheme.BgSurface,
            };
            footer.Controls.Add(save);
            footer.Controls.Add(cancel);

            Controls.Add(content);
            Controls.Add(footer);
        }

        public string SprintName => _name.Text.Trim();
        public string? Goal => string.IsNullOrWhiteSpace(_goal.Text) ? null : _goal.Text.Trim();
    }

    private sealed class CloseSprintDialog : Form
    {
        private readonly RadioButton _moveToBacklog = new() { Text = "Move incomplete issues to backlog", Checked = true, AutoSize = true, Font = JiraTheme.FontBody };
        private readonly RadioButton _moveToSprint = new() { Text = "Move incomplete issues to another sprint", AutoSize = true, Font = JiraTheme.FontBody };
        private readonly ComboBox _targetSprint = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody };

        public CloseSprintDialog(Sprint sprint, IReadOnlyList<Sprint> candidateSprints)
        {
            Text = $"Close {sprint.Name}";
            AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
            Width = 460;
            Height = 220;
            MinimumSize = new Size(460, 220);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = JiraTheme.BgSurface;
            Font = JiraTheme.FontBody;

            _targetSprint.DataSource = candidateSprints.ToList();
            _targetSprint.DisplayMember = nameof(Sprint.Name);
            _targetSprint.ValueMember = nameof(Sprint.Id);
            _targetSprint.Enabled = false;

            _moveToBacklog.CheckedChanged += (_, _) => _targetSprint.Enabled = _moveToSprint.Checked;
            _moveToSprint.CheckedChanged += (_, _) => _targetSprint.Enabled = _moveToSprint.Checked;

            var okButton = JiraControlFactory.CreatePrimaryButton("Close Sprint");
            var cancelButton = JiraControlFactory.CreateSecondaryButton("Cancel");
            okButton.Click += (_, _) =>
            {
                MoveIncompleteToSprintId = _moveToSprint.Checked && _targetSprint.SelectedValue is int sprintId ? sprintId : null;
                DialogResult = DialogResult.OK;
                Close();
            };
            cancelButton.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = JiraTheme.BgSurface };
            actions.Controls.Add(okButton);
            actions.Controls.Add(cancelButton);

            var content = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16), BackColor = JiraTheme.BgSurface };
            content.Controls.Add(JiraControlFactory.CreateLabel("Choose where incomplete issues should go when this sprint closes.", true));
            content.Controls.Add(_moveToBacklog);
            content.Controls.Add(_moveToSprint);
            content.Controls.Add(_targetSprint);

            Controls.Add(content);
            Controls.Add(actions);
        }

        public int? MoveIncompleteToSprintId { get; private set; }
    }
}








