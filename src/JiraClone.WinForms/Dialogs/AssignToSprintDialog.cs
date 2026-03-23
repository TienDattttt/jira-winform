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
        IntegralHeight = false,
    };

    public AssignToSprintDialog(Sprint sprint, IReadOnlyList<Issue> issues)
    {
        Sprint = sprint;
        Issues = issues;

        Text = $"Assign Issues To {sprint.Name}";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 560);
        MinimumSize = new Size(760, 560);
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;

        var title = JiraControlFactory.CreateLabel($"Assign issues to {sprint.Name}");
        title.Font = JiraTheme.FontH2;
        title.Dock = DockStyle.Top;
        title.Height = 40;

        var summaryLabel = JiraControlFactory.CreateLabel($"Select one or more issues for sprint '{sprint.Name}'.", true);
        summaryLabel.Dock = DockStyle.Top;
        summaryLabel.Height = 28;

        var assignButton = JiraControlFactory.CreatePrimaryButton("Assign");
        var cancelButton = JiraControlFactory.CreateSecondaryButton("Cancel");
        assignButton.AutoSize = false;
        assignButton.Size = new Size(104, 40);
        cancelButton.AutoSize = false;
        cancelButton.Size = new Size(104, 40);
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

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 68,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(20, 12, 20, 12),
            BackColor = JiraTheme.BgSurface,
        };
        footer.Controls.Add(assignButton);
        footer.Controls.Add(cancelButton);

        var listHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(0, 12, 0, 0),
        };
        listHost.Controls.Add(_issuesList);

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            BackColor = JiraTheme.BgSurface,
        };
        body.Controls.Add(listHost);
        body.Controls.Add(summaryLabel);
        body.Controls.Add(title);

        _issuesList.DisplayMember = nameof(Issue.Title);
        foreach (var issue in issues.OrderBy(x => x.WorkflowStatus.DisplayOrder).ThenBy(x => x.Title))
        {
            var index = _issuesList.Items.Add(issue);
            if (issue.SprintId == sprint.Id)
            {
                _issuesList.SetItemChecked(index, true);
            }
        }

        Controls.Add(body);
        Controls.Add(footer);
    }

    public Sprint Sprint { get; }
    public IReadOnlyList<Issue> Issues { get; }
    public IReadOnlyList<int> SelectedIssueIds { get; private set; } = Array.Empty<int>();
}


