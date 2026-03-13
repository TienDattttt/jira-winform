using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Controls;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public class IssueEditorForm : Form
{
    private readonly AppSession _session;
    private readonly int _projectId;
    private readonly int? _issueId;
    private readonly IssueStatus? _defaultStatus;
    private readonly TextBox _titleTextBox = JiraControlFactory.CreateTextBox();
    private readonly TextBox _descriptionTextBox = JiraControlFactory.CreateTextBox();
    private readonly ComboBox _typeComboBox = CreateCombo();
    private readonly ComboBox _statusComboBox = CreateCombo();
    private readonly ComboBox _priorityComboBox = CreateCombo();
    private readonly ComboBox _reporterComboBox = CreateCombo();
    private readonly CheckedListBox _assigneesList = new()
    {
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.None,
        BackColor = JiraTheme.BgSurface,
        ForeColor = JiraTheme.TextPrimary,
        Font = JiraTheme.FontBody,
        CheckOnClick = true,
        IntegralHeight = false,
    };
    private readonly SprintSelectorControl _sprintSelector = new() { Dock = DockStyle.Fill, MinimumSize = new Size(0, 38) };
    private readonly Button _saveButton = JiraControlFactory.CreatePrimaryButton("Save issue");
    private readonly Button _cancelButton = JiraControlFactory.CreateSecondaryButton("Cancel");

    public IssueEditorForm(AppSession session, int projectId, int? issueId, IssueStatus? defaultStatus = null)
    {
        _session = session;
        _projectId = projectId;
        _issueId = issueId;
        _defaultStatus = defaultStatus;

        Text = issueId.HasValue ? "Edit Issue" : "Create Issue";
        AutoScaleMode = AutoScaleMode.Font;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 720);
        Size = new Size(820, 780);
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;

        _titleTextBox.MinimumSize = new Size(0, 38);
        _descriptionTextBox.Multiline = true;
        _descriptionTextBox.ScrollBars = ScrollBars.Vertical;
        _descriptionTextBox.MinimumSize = new Size(0, 120);
        _descriptionTextBox.Height = 120;

        _typeComboBox.DataSource = Enum.GetValues(typeof(IssueType));
        _statusComboBox.DataSource = Enum.GetValues(typeof(IssueStatus));
        _priorityComboBox.DataSource = Enum.GetValues(typeof(IssuePriority));

        _saveButton.AutoSize = false;
        _saveButton.Size = new Size(132, 40);
        _saveButton.Click += async (_, _) => await SaveAsync();

        _cancelButton.AutoSize = false;
        _cancelButton.Size = new Size(112, 40);
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        Controls.Add(BuildLayout());
        Shown += async (_, _) => await LoadDataAsync();
    }

    private Control BuildLayout()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            BackColor = JiraTheme.BgSurface,
        };

        var title = JiraControlFactory.CreateLabel(_issueId.HasValue ? "Edit issue" : "Create issue");
        title.Font = JiraTheme.FontH1;
        title.Dock = DockStyle.Top;
        title.Height = 52;

        var subtitle = JiraControlFactory.CreateLabel("Capture the core issue details without leaving the board flow.", true);
        subtitle.Dock = DockStyle.Top;
        subtitle.Height = 28;

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0),
            BackColor = JiraTheme.BgSurface,
        };
        footer.Controls.Add(_saveButton);
        footer.Controls.Add(_cancelButton);

        var formLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(0, 12, 0, 0),
        };
        formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 144));
        formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(formLayout, 0, "Title", WrapControl(_titleTextBox, 44));
        AddRow(formLayout, 1, "Description", WrapControl(_descriptionTextBox, 132));
        AddRow(formLayout, 2, "Type", WrapControl(_typeComboBox, 44));
        AddRow(formLayout, 3, "Status", WrapControl(_statusComboBox, 44));
        AddRow(formLayout, 4, "Priority", WrapControl(_priorityComboBox, 44));
        AddRow(formLayout, 5, "Reporter", WrapControl(_reporterComboBox, 44));
        AddRow(formLayout, 6, "Sprint", WrapControl(_sprintSelector, 44));
        AddRow(formLayout, 7, "Assignees", WrapControl(_assigneesList, 148));

        host.Controls.Add(formLayout);
        host.Controls.Add(footer);
        host.Controls.Add(subtitle);
        host.Controls.Add(title);
        return host;
    }

    private static Panel WrapControl(Control control, int height)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = height,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(0, 0, 0, 8),
        };
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0);
        panel.Controls.Add(control);
        return panel;
    }

    private static ComboBox CreateCombo() => new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        BackColor = JiraTheme.BgSurface,
        ForeColor = JiraTheme.TextPrimary,
        Font = JiraTheme.FontBody,
        MinimumSize = new Size(0, 38),
    };

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.RowCount = row + 1;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var labelControl = JiraControlFactory.CreateLabel(label, true);
        labelControl.TextAlign = ContentAlignment.MiddleLeft;
        labelControl.Dock = DockStyle.Top;
        labelControl.Padding = new Padding(0, 10, 0, 0);
        labelControl.Height = 32;

        layout.Controls.Add(labelControl, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var users = await _session.Users.GetProjectUsersAsync(_projectId);
            _reporterComboBox.DataSource = users.ToList();
            _reporterComboBox.DisplayMember = nameof(User.DisplayName);
            _reporterComboBox.ValueMember = nameof(User.Id);

            _assigneesList.Items.Clear();
            foreach (var user in users)
            {
                _assigneesList.Items.Add(user, false);
            }
            _assigneesList.DisplayMember = nameof(User.DisplayName);

            _sprintSelector.Bind(await _session.Sprints.GetByProjectAsync(_projectId));

            if (_issueId is null)
            {
                if (_defaultStatus.HasValue)
                {
                    _statusComboBox.SelectedItem = _defaultStatus.Value;
                }

                return;
            }

            var details = await _session.Issues.GetDetailsAsync(_issueId.Value);
            if (details is null)
            {
                ErrorDialogService.Show("Issue not found.");
                Close();
                return;
            }

            _titleTextBox.Text = details.Issue.Title;
            _descriptionTextBox.Text = details.Issue.DescriptionText;
            _typeComboBox.SelectedItem = details.Issue.Type;
            _statusComboBox.SelectedItem = details.Issue.Status;
            _priorityComboBox.SelectedItem = details.Issue.Priority;
            _reporterComboBox.SelectedValue = details.Issue.ReporterId;
            if (details.Issue.SprintId.HasValue)
            {
                _sprintSelector.SelectedValue = details.Issue.SprintId.Value;
            }

            for (var index = 0; index < _assigneesList.Items.Count; index++)
            {
                if (_assigneesList.Items[index] is not User user)
                {
                    continue;
                }

                _assigneesList.SetItemChecked(index, details.Issue.Assignees.Any(a => a.UserId == user.Id));
            }
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
            Close();
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            var currentUserId = _session.CurrentUserContext.CurrentUser?.Id ?? 1;
            var model = new IssueEditModel
            {
                Id = _issueId,
                ProjectId = _projectId,
                Title = _titleTextBox.Text,
                DescriptionText = _descriptionTextBox.Text,
                Type = (IssueType)_typeComboBox.SelectedItem!,
                Status = (IssueStatus)_statusComboBox.SelectedItem!,
                Priority = (IssuePriority)_priorityComboBox.SelectedItem!,
                ReporterId = _reporterComboBox.SelectedValue is int reporterId ? reporterId : 1,
                CreatedById = currentUserId,
                SprintId = _sprintSelector.SelectedValue is int sprintId ? sprintId : null,
                AssigneeIds = _assigneesList.CheckedItems.Cast<User>().Select(x => x.Id).ToArray()
            };

            if (_issueId is null)
            {
                await _session.Issues.CreateAsync(model);
            }
            else
            {
                await _session.Issues.UpdateAsync(model);
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }
}
