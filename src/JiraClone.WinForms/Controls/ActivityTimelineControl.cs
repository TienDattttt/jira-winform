using JiraClone.Domain.Entities;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public class ActivityTimelineControl : UserControl
{
    private readonly ListBox _listBox = new()
    {
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.None,
        BackColor = JiraTheme.BgSurface,
        ForeColor = JiraTheme.TextPrimary,
        Font = JiraTheme.FontSmall,
        HorizontalScrollbar = true,
        IntegralHeight = false,
    };

    public ActivityTimelineControl()
    {
        DoubleBuffered = true;
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;
        Padding = new Padding(JiraTheme.Padding);
        Controls.Add(_listBox);
    }

    public void Bind(IReadOnlyList<ActivityLog> activityLogs)
    {
        _listBox.Items.Clear();
        foreach (var log in activityLogs.OrderByDescending(x => x.OccurredAtUtc))
        {
            var value = string.IsNullOrWhiteSpace(log.NewValue) ? log.OldValue : log.NewValue;
            _listBox.Items.Add($"{log.OccurredAtUtc:g}  {log.ActionType}  {value}");
        }
    }
}
