using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Application = FlaUI.Core.Application;

namespace JiraClone.E2ETests.Infrastructure;

public sealed class AppDriver : IDisposable
{
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

    private readonly string _sessionFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "JiraDesktop",
        "session.dat");

    public AppDriver()
    {
        Config = TestConfig.Load();
        Automation = new UIA3Automation();
    }

    public TestConfig Config { get; }
    public UIA3Automation Automation { get; }
    public Application? Application { get; private set; }
    public Window? CurrentWindow { get; private set; }

    private string ProcessName => Path.GetFileNameWithoutExtension(Config.AppPath);

    public Window Launch()
    {
        if (Application is not null && !Application.HasExited)
        {
            CurrentWindow ??= ResolveTopLevelWindow(Config.StartupTimeoutMs);
            return CurrentWindow;
        }

        EnsureCleanSession();
        Application = Application.Launch(Config.AppPath);
        CurrentWindow = ResolveTopLevelWindow(Config.StartupTimeoutMs);
        FocusWindow(CurrentWindow);
        return CurrentWindow;
    }

    public Window RestartToLogin()
    {
        Quit();
        Launch();
        return WaitForLoginWindow();
    }

    public Window WaitForLoginWindow() =>
        WaitForWindow(IsLoginWindow, Config.StartupTimeoutMs, "Login form");

    public Window WaitForMainWindow() =>
        WaitForWindow(IsMainWindow, Config.StartupTimeoutMs, "Main form");

    public Window WaitForWindow(Func<Window, bool> predicate, int timeoutMs, string description)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            var window = TryFindWindow(predicate, 250);
            if (window is not null)
            {
                FocusWindow(window);
                CurrentWindow = window;
                return window;
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Timed out waiting for {description}.");
    }

    public Window WaitForWindowByTitle(string titleFragment, int timeoutMs = 5000) =>
        WaitForWindow(window => window.Title.Contains(titleFragment, StringComparison.OrdinalIgnoreCase), timeoutMs, $"window containing '{titleFragment}'");

    public Window WaitForWindowContainingElement(string automationId, int timeoutMs = 5000) =>
        WaitForWindow(window => TryFindElement(window, automationId, 50) is not null, timeoutMs, $"window containing element '{automationId}'");

    public Window? TryFindWindowByTitle(string titleFragment, int timeoutMs = 1500) =>
        TryFindWindow(window => window.Title.Contains(titleFragment, StringComparison.OrdinalIgnoreCase), timeoutMs);

    public Window? TryFindWindowContainingElement(string automationId, int timeoutMs = 1500) =>
        TryFindWindow(window => TryFindElement(window, automationId, 50) is not null, timeoutMs);

    public Window? TryFindWindow(Func<Window, bool> predicate, int timeoutMs = 1500)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            var window = TryFindWindowCore(predicate);
            if (window is not null)
            {
                return window;
            }

            Thread.Sleep(100);
        }

        return null;
    }

    public AutomationElement WaitForElement(AutomationElement root, string automationId, int timeoutMs = 5000)
    {
        var element = TryFindElement(root, automationId, timeoutMs);
        if (element is null)
        {
            throw new TimeoutException($"Timed out waiting for element '{automationId}'.");
        }

        return element;
    }

    public AutomationElement? TryFindElement(AutomationElement root, string automationId, int timeoutMs = 1500)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                var conditionFactory = root.Automation.ConditionFactory;
                var element = root.FindFirstDescendant(conditionFactory.ByAutomationId(automationId))
                    ?? root.FindFirstDescendant(conditionFactory.ByName(automationId));

                if (element is not null)
                {
                    return element;
                }
            }
            catch
            {
            }

            Thread.Sleep(100);
        }

        return null;
    }

    public AutomationElement? TryFindText(AutomationElement root, string text, int timeoutMs = 1500)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                var conditionFactory = root.Automation.ConditionFactory;
                var element = root.FindFirstDescendant(conditionFactory.ByName(text));
                if (element is not null)
                {
                    return element;
                }
            }
            catch
            {
            }

            Thread.Sleep(100);
        }

        return null;
    }

    public string CaptureWindow(string fileName)
    {
        var window = CurrentWindow ?? ResolveTopLevelWindow(Config.ActionTimeoutMs);
        FocusWindow(window);
        var bounds = window.BoundingRectangle;
        var width = Math.Max(1, (int)Math.Ceiling(Convert.ToDouble(bounds.Width)));
        var height = Math.Max(1, (int)Math.Ceiling(Convert.ToDouble(bounds.Height)));
        var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "screenshots");
        Directory.CreateDirectory(outputDirectory);

        using var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen((int)bounds.Left, (int)bounds.Top, 0, 0, new Size(width, height));

        var outputPath = Path.Combine(outputDirectory, fileName);
        bitmap.Save(outputPath, ImageFormat.Png);
        return outputPath;
    }

    public void LeftClickScreen(int x, int y)
    {
        SetCursorPos(x, y);
        Thread.Sleep(100);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(200);
    }

    public void Quit()
    {
        if (Application is null)
        {
            return;
        }

        try
        {
            if (!Application.HasExited)
            {
                Application.Close();
            }
        }
        catch
        {
        }

        try
        {
            if (!Application.HasExited)
            {
                Application.Kill();
            }
        }
        catch
        {
        }

        Application.Dispose();
        Application = null;
        CurrentWindow = null;
    }

    public void Dispose()
    {
        Quit();
        Automation.Dispose();
    }

    private void EnsureCleanSession()
    {
        if (File.Exists(_sessionFilePath))
        {
            File.Delete(_sessionFilePath);
        }
    }

    private Window ResolveTopLevelWindow(int timeoutMs)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            var window = TryFindWindowCore(_ => true);
            if (window is not null)
            {
                return window;
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException("Timed out waiting for the application window.");
    }

    private bool IsLoginWindow(Window window)
    {
        if (window.Title.Contains("Jira Clone Login", StringComparison.OrdinalIgnoreCase)
            || window.Title.Contains("Dang nhap Jira Clone", StringComparison.OrdinalIgnoreCase)
            || window.Title.Contains("Dang nhap", StringComparison.OrdinalIgnoreCase)
            || window.Title.Contains("Ðang nh?p Jira Clone", StringComparison.OrdinalIgnoreCase)
            || window.Title.Contains("Ðang nh?p", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return TryFindElement(window, "LoginForm_TextBox_Email", 100) is not null
            && TryFindElement(window, "LoginForm_Button_Login", 100) is not null;
    }

    private bool IsMainWindow(Window window)
    {
        if (window.Title.Contains("Jira Clone Desktop", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return TryFindElement(window, "MainForm_TextBox_Search", 100) is not null
            || TryFindElement(window, "MainForm_Button_Notification", 100) is not null;
    }

    private void FocusWindow(Window? window)
    {
        if (window is null)
        {
            return;
        }

        try
        {
            var handle = window.Properties.NativeWindowHandle.ValueOrDefault;
            if (handle != 0)
            {
                ShowWindow(handle, 9);
                SetForegroundWindow(handle);
            }

            window.Focus();
        }
        catch
        {
        }
    }

    private bool TryAttachToRunningProcess()
    {
        try
        {
            var candidate = Process.GetProcessesByName(ProcessName)
                .Where(IsProcessAlive)
                .OrderByDescending(GetProcessStartTimeUtc)
                .FirstOrDefault();
            if (candidate is null)
            {
                return false;
            }

            if (Application is not null && !Application.HasExited && Application.ProcessId == candidate.Id)
            {
                return true;
            }

            try
            {
                Application?.Dispose();
            }
            catch
            {
            }

            Application = Application.Attach(candidate.Id);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsProcessAlive(Process process)
    {
        try
        {
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static DateTime GetProcessStartTimeUtc(Process process)
    {
        try
        {
            return process.StartTime.ToUniversalTime();
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private Window? TryFindWindowCore(Func<Window, bool> predicate)
    {
        if (Application is null || Application.HasExited)
        {
            TryAttachToRunningProcess();
        }

        var windows = GetCandidateWindows();
        if (windows.Count == 0 && TryAttachToRunningProcess())
        {
            windows = GetCandidateWindows();
        }

        return windows.FirstOrDefault(predicate);
    }

    private List<Window> GetCandidateWindows()
    {
        if (Application is null)
        {
            return [];
        }

        try
        {
            return Application.GetAllTopLevelWindows(Automation)
                .Where(window => !string.IsNullOrWhiteSpace(window.Title)
                    || TryFindElement(window, "LoginForm_TextBox_Email", 50) is not null
                    || TryFindElement(window, "MainForm_TextBox_Search", 50) is not null)
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
