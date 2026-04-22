using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public class SprintSelectorControl : ComboBox
{
    public SprintSelectorControl()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        BackColor = JiraTheme.BgSurface;
        ForeColor = JiraTheme.TextPrimary;
        Font = JiraTheme.FontBody;
        Margin = new Padding(JiraTheme.Padding);
        Height = 36;
        MinimumSize = new Size(120, 36);
        IntegralHeight = false;
        Anchor = AnchorStyles.Left | AnchorStyles.Right;
    }

    public void Bind(IReadOnlyList<Sprint> sprints)
    {
        Bind(sprints, selectedSprintId: null, includeClosed: true, includeEmpty: false);
    }

    public void Bind(
        IReadOnlyList<Sprint> sprints,
        int? selectedSprintId,
        bool includeClosed = true,
        bool includeEmpty = true,
        string emptyLabel = "(Không thuộc sprint)")
    {
        var options = (sprints ?? Array.Empty<Sprint>())
            .Where(sprint => !sprint.IsDeleted)
            .Where(sprint => includeClosed || sprint.State != SprintState.Closed || sprint.Id == selectedSprintId)
            .OrderByDescending(sprint => sprint.State == SprintState.Active)
            .ThenByDescending(sprint => sprint.State == SprintState.Planned)
            .ThenByDescending(sprint => sprint.StartDate)
            .ThenBy(sprint => sprint.Name, StringComparer.OrdinalIgnoreCase)
            .Select(sprint => new SprintOption(sprint.Id, sprint.Name))
            .ToList();

        if (includeEmpty)
        {
            options.Insert(0, new SprintOption(0, emptyLabel));
        }

        DataSource = null;
        DisplayMember = nameof(SprintOption.DisplayName);
        ValueMember = nameof(SprintOption.Id);
        DataSource = options;

        if (selectedSprintId.HasValue && options.Any(option => option.Id == selectedSprintId.Value))
        {
            SelectedValue = selectedSprintId.Value;
            return;
        }

        if (includeEmpty)
        {
            SelectedValue = 0;
            return;
        }

        SelectedIndex = options.Count == 0 ? -1 : 0;
    }

    private sealed record SprintOption(int Id, string DisplayName);
}

