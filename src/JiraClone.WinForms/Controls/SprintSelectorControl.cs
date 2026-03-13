using JiraClone.Domain.Entities;
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
        DataSource = sprints.ToList();
        DisplayMember = nameof(Sprint.Name);
        ValueMember = nameof(Sprint.Id);
    }
}
