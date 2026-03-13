using System.Drawing.Drawing2D;
using JiraClone.Domain.Enums;

namespace JiraClone.WinForms.Theme;

public static class JiraIcons
{
    public static Bitmap GetIssueTypeIcon(IssueType type, int size = 16)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        switch (type)
        {
            case IssueType.Story:
                using (var brush = new SolidBrush(Color.FromArgb(0x4B, 0xCE, 0x97)))
                {
                    var points = new[]
                    {
                        new Point(size / 2, 1),
                        new Point(size - 2, size / 3),
                        new Point(size - 5, size - 2),
                        new Point(size / 2, size - 5),
                        new Point(4, size - 2),
                        new Point(1, size / 3)
                    };
                    graphics.FillPolygon(brush, points);
                }
                break;
            case IssueType.Bug:
                using (var brush = new SolidBrush(Color.FromArgb(0xF8, 0x71, 0x68)))
                {
                    graphics.FillEllipse(brush, 2, 3, size - 4, size - 6);
                    using var head = new SolidBrush(JiraTheme.Red700);
                    graphics.FillEllipse(head, size / 2 - 3, 1, 6, 6);
                }
                break;
            case IssueType.Task:
                using (var pen = new Pen(Color.FromArgb(0x66, 0x9D, 0xF1), 2f))
                {
                    graphics.DrawRectangle(pen, 2, 2, size - 5, size - 5);
                    graphics.DrawLine(pen, 4, size / 2, size / 2 - 1, size - 5);
                    graphics.DrawLine(pen, size / 2 - 1, size - 5, size - 3, 4);
                }
                break;
            default:
                using (var pen = new Pen(Color.FromArgb(0xC9, 0x7C, 0xF4), 2f))
                {
                    graphics.DrawLine(pen, size / 2, 1, size / 2 - 2, size / 2);
                    graphics.DrawLine(pen, size / 2 - 2, size / 2, size / 2 + 1, size / 2);
                    graphics.DrawLine(pen, size / 2 + 1, size / 2, size / 2 - 1, size - 2);
                    graphics.DrawLine(pen, size / 2 - 1, size - 2, size / 2 + 4, size / 2 + 2);
                }
                break;
        }

        return bitmap;
    }

    public static Bitmap GetPriorityIcon(IssuePriority priority, int size = 16)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var color = priority switch
        {
            IssuePriority.Highest => JiraTheme.Red700,
            IssuePriority.High => Color.FromArgb(0xF8, 0x71, 0x68),
            IssuePriority.Medium => JiraTheme.Orange400,
            IssuePriority.Low => Color.FromArgb(0x4B, 0xCE, 0x97),
            _ => JiraTheme.Green500
        };

        using var pen = new Pen(color, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        switch (priority)
        {
            case IssuePriority.Highest:
                DrawUpArrow(graphics, pen, size, 3);
                DrawUpArrow(graphics, pen, size, 7);
                break;
            case IssuePriority.High:
                DrawUpArrow(graphics, pen, size, 5);
                break;
            case IssuePriority.Medium:
                graphics.DrawLine(pen, 3, 6, size - 3, 6);
                graphics.DrawLine(pen, 3, 10, size - 3, 10);
                break;
            case IssuePriority.Low:
                DrawDownArrow(graphics, pen, size, 5);
                break;
            default:
                DrawDownArrow(graphics, pen, size, 3);
                DrawDownArrow(graphics, pen, size, 7);
                break;
        }

        return bitmap;
    }

    private static void DrawUpArrow(Graphics graphics, Pen pen, int size, int top)
    {
        graphics.DrawLine(pen, size / 2, top, 4, top + 5);
        graphics.DrawLine(pen, size / 2, top, size - 4, top + 5);
    }

    private static void DrawDownArrow(Graphics graphics, Pen pen, int size, int top)
    {
        graphics.DrawLine(pen, 4, top, size / 2, top + 5);
        graphics.DrawLine(pen, size - 4, top, size / 2, top + 5);
    }
}
