using System.Drawing.Drawing2D;

namespace JiraClone.WinForms.Helpers;

public static class GraphicsHelper
{
    /// <summary>
    /// Creates a rounded rectangle <see cref="GraphicsPath"/>.
    /// The caller is responsible for disposing the returned path.
    /// </summary>
    public static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var diameter = Math.Max(2, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
