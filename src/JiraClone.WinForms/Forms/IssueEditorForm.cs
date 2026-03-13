using JiraClone.Application.Models;
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
    private readonly TextBox _titleTextBox = JiraControlFactory.CreateTextBox();
    private readonly TextBox _descriptionTextBox = JiraControlFactory.CreateTextBox();
    private readonly ComboBox _typeComboBox = CreateCombo();
    private readonly ComboBox _statusComboBox = CreateCombo();
    private readonly ComboBox _priorityComboBox = CreateCombo();
    private readonly ComboBox _reporterComboBox = CreateCombo();
    private readonly CheckedListBox _assigneesList = new() { Dock = DockStyle.Fill, Height = 120, BorderStyle = BorderStyle.None, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody };
    private readonly SprintSelectorControl _sprintSelector = new() { Dock = DockStyle.Fill };

    public IssueEditorForm(AppSession session, int projectId, int? issueId)
    {
        _session = session;
        _projectId = projectId;
        _issueId = issueId;

        Text = issueId.HasValue ? "Edit Issue" : "Create Issue";
        AutoScaleMode = AutoScaleMode.Font;
        Width = 700;
        Height = 680;
        MinimumSize = new Size(640, 620);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;

        _descriptionTextBox.Multiline = true;
        _descriptionTextBox.Height = 120;
        _descriptionTextBox.ScrollBars = ScrollBars.Vertical;

        _typeComboBox.DataSource = Enum.GetValues(typeof(IssueType));
        _statusComboBox.DataSource = Enum.GetValues(typeof(IssueStatus));
        _priorityComboBox.DataSource = Enum.GetValues(typeof(IssuePriority));

        var title = JiraControlFactory.CreateLabel(issueId.HasValue ? "Edit issue" : "Create issue");
        title.Font = JiraTheme.FontH2;
        title.Dock = DockStyle.Top;

        var saveButton = JiraControlFactory.CreatePrimaryButton("Save");
        saveButton.AutoSize = false;
        saveButton.MinimumSize = new Size(120, 40);
        saveButton.Dock = DockStyle.Fill;
        saveButton.Click += async (_, _) => await SaveAsync();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(20),
            BackColor = JiraTheme.BgSurface,
            AutoScroll = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(layout, 0, "Title", _titleTextBox);
        AddRow(layout, 1, "Description", _descriptionTextBox);
        AddRow(layout, 2, "Type", _typeComboBox);
        AddRow(layout, 3, "Status", _statusComboBox);
        AddRow(layout, 4, "Priority", _priorityComboBox);
        AddRow(layout, 5, "Reporter", _reporterComboBox);
        AddRow(layout, 6, "Sprint", _sprintSelector);
        AddRow(layout, 7, "Assignees", _assigneesList);
        layout.Controls.Add(saveButton, 1, 8);

        Controls.Add(layout);
        Controls.Add(title);
        Shown += async (_, _) => await LoadDataAsync();
    }

    private static ComboBox CreateCombo() => new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        BackColor = JiraTheme.BgSurface,
        ForeColor = JiraTheme.TextPrimary,
        Font = JiraTheme.FontBody
    };

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.Controls.Add(JiraControlFactory.CreateLabel(label, true), 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private async Task LoadDataAsync()
    {
        var users = await _session.Users.GetProjectUsersAsync(_projectId);
        _reporterComboBox.DataSource = users.ToList();
        _reporterComboBox.DisplayMember = "DisplayName";
        _reporterComboBox.ValueMember = "Id";

        _assigneesList.Items.Clear();
        foreach (var user in users)
        {
            _assigneesList.Items.Add(user, false);
        }
        _assigneesList.DisplayMember = "DisplayName";

        _sprintSelector.Bind(await _session.Sprints.GetByProjectAsync(_projectId));

        if (_issueId is null)
        {
            return;
        }

        var details = await _session.Issues.GetDetailsAsync(_issueId.Value);
        if (details is null)
        {
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
            if (_assigneesList.Items[index] is not JiraClone.Domain.Entities.User user)
            {
                continue;
            }

            _assigneesList.SetItemChecked(index, details.Issue.Assignees.Any(a => a.UserId == user.Id));
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
                AssigneeIds = _assigneesList.CheckedItems.Cast<JiraClone.Domain.Entities.User>().Select(x => x.Id).ToArray()
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
