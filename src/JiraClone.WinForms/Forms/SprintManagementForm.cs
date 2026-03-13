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
    private readonly ListView _listView = new() { Dock = DockStyle.Fill, FullRowSelect = true, MultiSelect = false, View = View.Details, BorderStyle = BorderStyle.None, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody };
    private readonly Button _createButton = JiraControlFactory.CreatePrimaryButton("Create Sprint");
    private readonly Button _assignButton = JiraControlFactory.CreateSecondaryButton("Assign Issues");
    private readonly Button _startButton = JiraControlFactory.CreateSecondaryButton("Start Sprint");
    private readonly Button _closeButton = JiraControlFactory.CreateSecondaryButton("Close Sprint");
    private readonly Label _helpLabel = JiraControlFactory.CreateLabel(string.Empty, true);
    private List<Sprint> _sprints = [];
    private int _projectId;
    private bool _isLoading;

    public SprintManagementForm(AppSession session)
    {
        _session = session;
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;

        _listView.Columns.Add("Sprint", 220);
        _listView.Columns.Add("State", 120);
        _listView.Columns.Add("Start", 140);
        _listView.Columns.Add("End", 140);
        _listView.Columns.Add("Goal", 420);

        _createButton.Click += async (_, _) => await CreateSprintAsync();
        _assignButton.Click += async (_, _) => await AssignIssuesAsync();
        _startButton.Click += async (_, _) => await StartSprintAsync();
        _closeButton.Click += async (_, _) => await CloseSprintAsync();
        ConfigureActionButton(_createButton, 132);
        ConfigureActionButton(_assignButton, 126);
        ConfigureActionButton(_startButton, 116);
        ConfigureActionButton(_closeButton, 116);

        var header = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = JiraTheme.BgPage, Padding = new Padding(16, 16, 16, 8) };
        var title = JiraControlFactory.CreateLabel("Sprints");
        title.Font = JiraTheme.FontH2;
        title.Location = new Point(0, 0);
        _helpLabel.Location = new Point(0, 36);
        header.Controls.Add(title);
        header.Controls.Add(_helpLabel);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 62,
            Padding = new Padding(12, 8, 12, 10),
            BackColor = JiraTheme.BgPage
        };
        actions.Controls.AddRange([_createButton, _assignButton, _startButton, _closeButton]);

        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 0, 16, 16), BackColor = JiraTheme.BgPage };
        host.Controls.Add(_listView);

        Controls.Add(host);
        Controls.Add(actions);
        Controls.Add(header);

        Load += async (_, _) => await LoadSprintsAsync();
    }

    private Sprint? SelectedSprint =>
        _listView.SelectedIndices.Count == 0 ? null : _sprints[_listView.SelectedIndices[0]];

    private async Task LoadSprintsAsync()
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
                project = await _session.Projects.GetActiveProjectAsync();
                if (project is null)
                {
                    return;
                }

                sprints = (await _session.Sprints.GetByProjectAsync(project.Id)).ToList();
            });

            if (project is null)
            {
                return;
            }

            _projectId = project.Id;
            _sprints = sprints;
            _listView.Items.Clear();
            foreach (var sprint in _sprints)
            {
                var item = new ListViewItem(sprint.Name);
                item.SubItems.Add(sprint.State.ToString());
                item.SubItems.Add(sprint.StartDate?.ToString("dd MMM yyyy") ?? "-");
                item.SubItems.Add(sprint.EndDate?.ToString("dd MMM yyyy") ?? "-");
                item.SubItems.Add(sprint.Goal ?? string.Empty);
                _listView.Items.Add(item);
            }

            var activeSprint = _sprints.FirstOrDefault(x => x.State == SprintState.Active);
            _helpLabel.Text = activeSprint is null ? "No active sprint." : $"Active sprint: {activeSprint.Name}";
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

    private static void ConfigureActionButton(Button button, int width)
    {
        button.AutoSize = false;
        button.Width = width;
        button.Height = 40;
    }

    private async Task CreateSprintAsync()
    {
        try
        {
            if (_projectId == 0)
            {
                return;
            }

            var name = Microsoft.VisualBasic.Interaction.InputBox("Sprint name", "Create Sprint", "New Sprint");
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            await _session.Sprints.CreateAsync(_projectId, name, null, null, null);
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

    private sealed class CloseSprintDialog : Form
    {
        private readonly RadioButton _moveToBacklog = new() { Text = "Move incomplete issues to backlog", Checked = true, AutoSize = true, Font = JiraTheme.FontBody };
        private readonly RadioButton _moveToSprint = new() { Text = "Move incomplete issues to another sprint", AutoSize = true, Font = JiraTheme.FontBody };
        private readonly ComboBox _targetSprint = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody };

        public CloseSprintDialog(Sprint sprint, IReadOnlyList<Sprint> candidateSprints)
        {
            Text = $"Close {sprint.Name}";
            AutoScaleMode = AutoScaleMode.Font;
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
