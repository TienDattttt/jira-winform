using JiraClone.Domain.Entities;
using JiraClone.WinForms.Helpers;
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
        Visible = false,
    };

    private readonly Label _emptyState = JiraControlFactory.CreateLabel("No activity yet.", true);

    public ActivityTimelineControl()
    {
        DoubleBuffered = true;
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;
        Padding = new Padding(JiraTheme.Padding);

        _emptyState.Dock = DockStyle.Fill;
        _emptyState.TextAlign = ContentAlignment.MiddleCenter;
        _emptyState.ForeColor = JiraTheme.TextSecondary;

        Controls.Add(_listBox);
        Controls.Add(_emptyState);
    }

    public void Bind(IReadOnlyList<ActivityLog> activityLogs)
    {
        _listBox.Items.Clear();
        foreach (var log in activityLogs.OrderByDescending(x => x.OccurredAtUtc))
        {
            var value = string.IsNullOrWhiteSpace(log.NewValue) ? log.OldValue : log.NewValue;
            _listBox.Items.Add($"{UtcDateTimeHelper.FormatLocal(log.OccurredAtUtc, "g")}  {log.ActionType}  {value}");
        }

        var hasItems = _listBox.Items.Count > 0;
        _listBox.Visible = hasItems;
        _emptyState.Visible = !hasItems;
    }
}
