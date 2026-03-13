using JiraClone.Domain.Entities;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Dialogs;

public class AssignToSprintDialog : Form
{
    private readonly CheckedListBox _issuesList = new()
    {
        Dock = DockStyle.Fill,
        CheckOnClick = true,
        BorderStyle = BorderStyle.None,
        BackColor = JiraTheme.BgSurface,
        ForeColor = JiraTheme.TextPrimary,
        Font = JiraTheme.FontBody,
    };

    public AssignToSprintDialog(Sprint sprint, IReadOnlyList<Issue> issues)
    {
        Sprint = sprint;
        Issues = issues;

        Text = $"Assign Issues To {sprint.Name}";
        AutoScaleMode = AutoScaleMode.Font;
        Width = 640;
        Height = 500;
        MinimumSize = new Size(640, 500);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;

        var summaryLabel = JiraControlFactory.CreateLabel($"Select issues to assign to sprint '{sprint.Name}'.", true);
        summaryLabel.Dock = DockStyle.Top;
        summaryLabel.Height = 36;

        var assignButton = JiraControlFactory.CreatePrimaryButton("Assign");
        var cancelButton = JiraControlFactory.CreateSecondaryButton("Cancel");
        assignButton.AutoSize = false;
        assignButton.Size = new Size(92, 36);
        cancelButton.AutoSize = false;
        cancelButton.Size = new Size(92, 36);
        assignButton.Click += (_, _) =>
        {
            SelectedIssueIds = _issuesList.CheckedItems.Cast<Issue>().Select(x => x.Id).ToArray();
            DialogResult = DialogResult.OK;
            Close();
        };
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12),
            BackColor = JiraTheme.BgSurface
        };
        buttons.Controls.Add(assignButton);
        buttons.Controls.Add(cancelButton);

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            BackColor = JiraTheme.BgSurface
        };
        body.Controls.Add(_issuesList);
        body.Controls.Add(summaryLabel);

        _issuesList.DisplayMember = nameof(Issue.Title);
        foreach (var issue in issues.OrderBy(x => x.Status).ThenBy(x => x.Title))
        {
            var index = _issuesList.Items.Add(issue);
            if (issue.SprintId == sprint.Id)
            {
                _issuesList.SetItemChecked(index, true);
            }
        }

        Controls.Add(body);
        Controls.Add(buttons);
    }

    public Sprint Sprint { get; }
    public IReadOnlyList<Issue> Issues { get; }
    public IReadOnlyList<int> SelectedIssueIds { get; private set; } = Array.Empty<int>();
}
