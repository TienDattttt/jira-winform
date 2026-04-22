using System.ComponentModel;
using System.Diagnostics;

namespace JiraClone.WinForms.Services;

public static class ApplicationHelpService
{
    public const string HelpFileName = "JiraClone.chm";
    private const string HelpDirectoryName = "Help";

    public static void ShowMainHelp(Control owner)
    {
        var helpFilePath = ResolveHelpFilePath();

        try
        {
            Help.ShowHelp(owner, helpFilePath);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            OpenWithHtmlHelpExecutable(helpFilePath);
        }
    }

    public static string ResolveHelpFilePath()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, HelpDirectoryName, HelpFileName),
            Path.Combine(baseDirectory, HelpFileName)
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Không tìm thấy file trợ giúp JiraClone.chm trong thư mục cài đặt.");
    }

    private static void OpenWithHtmlHelpExecutable(string helpFilePath)
    {
        var viewerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "hh.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = viewerPath,
            Arguments = $"\"{helpFilePath}\"",
            UseShellExecute = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Không thể mở file trợ giúp bằng HTML Help Viewer.");
        }
    }
}
