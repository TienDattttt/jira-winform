using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using JiraClone.E2ETests.Infrastructure;

namespace JiraClone.E2ETests.Pages;

public sealed class MainPage : PageBase
{
    private const int SidebarWidthEstimate = 240;
    private const int SidebarNavTopOffset = 76;
    private const int SidebarNavRowStep = 38;
    private const int SidebarButtonCenterOffsetX = 86;

    public MainPage(Window window, AppDriver driver) : base(window, driver)
    {
    }

    public bool HasNavigationItem(string automationId, string fallbackText, int timeoutMs = 1500) =>
        TryFind(automationId, timeoutMs) is not null || TryFindSidebarText(new[] { fallbackText }, timeoutMs) is not null;

    public bool HasUsersMenu(int timeoutMs = 1500)
    {
        if (TryFind("MainForm_Nav_Users", timeoutMs) is not null || TryFindSidebarText(new[] { "Ngu?i dùng", "Users" }, timeoutMs) is not null)
        {
            return true;
        }

        return TryOpenUsers(timeoutMs) is not null;
    }

    public bool HasUnexpectedErrorDialog(int timeoutMs = 1000) =>
        Driver.TryFindWindowByTitle("Unexpected Error", timeoutMs) is not null;

    public void Search(string value) => FindTextBox("MainForm_TextBox_Search").Enter(value);

    public void ClickCreateIssue() => FindButton("MainForm_Button_CreateIssue").Click();

    public bool IsCreateIssueVisible(int timeoutMs = 1000) => TryFind("MainForm_Button_CreateIssue", timeoutMs) is not null;

    public void OpenNotifications() => FindButton("MainForm_Button_Notification").Click();

    public ProjectListPage OpenProjects()
    {
        ClickNavigation("MainForm_Nav_Projects", new[] { "D? án", "Projects" }, 0);
        return new ProjectListPage(Driver.WaitForMainWindow(), Driver);
    }

    public MainPage OpenDashboard()
    {
        ClickNavigation("MainForm_Nav_Dashboard", new[] { "T?ng quan", "Dashboard" }, 1);
        return this;
    }

    public MainPage OpenBoard()
    {
        bool IsBoardPageVisible() =>
            TryFindSidebarText(Array.Empty<string>(), 10) is null && TryFindText("Nhóm theo Epic", 250) is not null;

        ClickNavigation("MainForm_Nav_Board", new[] { "B?ng", "Board" }, 2, successCheck: IsBoardPageVisible);
        return this;
    }

    public MainPage OpenBacklog()
    {
        ClickNavigation("MainForm_Nav_Backlog", new[] { "Backlog" }, 3);
        return this;
    }

    public MainPage OpenRoadmap()
    {
        ClickNavigation("MainForm_Nav_Roadmap", new[] { "L? trình", "Roadmap" }, 4);
        return this;
    }

    public SprintManagementPage OpenSprints()
    {
        bool IsSprintPageVisible() =>
            TryFind("SprintMgmt_Button_Create", 250) is not null
            || TryFindText("Sprint dang ho?t d?ng", 250) is not null
            || TryFindText("T?o sprint", 250) is not null;

        ClickNavigation("MainForm_Nav_Sprints", new[] { "Sprint" }, 5, successCheck: IsSprintPageVisible);
        return new SprintManagementPage(Driver.WaitForMainWindow(), Driver);
    }

    public IssueNavigatorPage OpenIssues()
    {
        bool IsIssuePageVisible() =>
            TryFind("IssueNav_TextBox_Search", 250) is not null
            || TryFindText("B? l?c", 250) is not null
            || TryFindText("T?o issue", 250) is not null;

        ClickNavigation("MainForm_Nav_Issues", new[] { "Issue", "Issues" }, 6, successCheck: IsIssuePageVisible);
        return new IssueNavigatorPage(Driver.WaitForMainWindow(), Driver);
    }

    public MainPage OpenReports()
    {
        ClickNavigation("MainForm_Nav_Reports", new[] { "Báo cáo", "Reports" }, 7);
        return this;
    }

    public UserManagementPage OpenUsers()
    {
        if (TryOpenUsers(6000) is { } page)
        {
            return page;
        }

        throw new TimeoutException("Timed out waiting for the User Management page.");
    }

    public MainPage OpenSettings()
    {
        ClickNavigation("MainForm_Nav_Settings", new[] { "Cài d?t", "Settings" }, 9);
        return this;
    }

    public LoginPage Logout()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var logout = TryFind("MainForm_Button_Logout", 1000)
                ?? TryFindSidebarText(new[] { "Ðang xu?t", "Logout" }, 1000)
                ?? TryFindSidebarButton(new[] { "Ðang xu?t", "Logout" }, 1000);

            if (logout is not null)
            {
                ClickElement(logout);
            }
            else
            {
                TryClickLogout();
            }

            ConfirmLogoutIfNeeded();

            var loginWindow = Driver.TryFindWindowContainingElement("LoginForm_TextBox_Email", 5000);
            if (loginWindow is not null)
            {
                return new LoginPage(loginWindow, Driver);
            }
        }

        return new LoginPage(Driver.WaitForLoginWindow(), Driver);
    }

    public UserManagementPage? TryOpenUsers(int timeoutMs = 1500)
    {
        bool IsUsersPageVisible() =>
            TryFind("UserMgmt_ListView_Users", 250) is not null
            || TryFindText("Display Name", 250) is not null
            || TryFindText("User Name", 250) is not null
            || TryFindText("Roles", 250) is not null;

        if (IsUsersPageVisible())
        {
            return new UserManagementPage(Driver.WaitForMainWindow(), Driver);
        }

        ClickNavigation(
            "MainForm_Nav_Users",
            new[] { "Ngu?i dùng", "Users" },
            8,
            allowFailure: true,
            successCheck: IsUsersPageVisible,
            timeoutMs: timeoutMs);

        return IsUsersPageVisible()
            ? new UserManagementPage(Driver.WaitForMainWindow(), Driver)
            : null;
    }

    private void ClickNavigation(
        string automationId,
        string[] fallbackTexts,
        int rowIndex,
        bool allowFailure = false,
        Func<bool>? successCheck = null,
        int timeoutMs = 5000)
    {
        var element = TryFind(automationId, 800) ?? TryFindSidebarText(fallbackTexts, 800);
        if (element is not null)
        {
            ClickElement(element);
            if (WaitForSuccess(successCheck, 1000))
            {
                return;
            }
        }

        TryClickSidebarRow(rowIndex, successCheck);
        if (WaitForSuccess(successCheck, timeoutMs))
        {
            return;
        }

        if (!allowFailure && successCheck is not null)
        {
            throw new TimeoutException($"Timed out waiting for navigation target '{automationId}'.");
        }
    }

    private bool WaitForSuccess(Func<bool>? successCheck, int timeoutMs)
    {
        if (successCheck is null)
        {
            return true;
        }

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (successCheck())
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return false;
    }

    private AutomationElement? TryFindSidebarText(IEnumerable<string> texts, int timeoutMs)
    {
        var names = texts.Where(text => !string.IsNullOrWhiteSpace(text)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (names.Length == 0)
        {
            return null;
        }

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var candidates = Window.FindAllDescendants()
                    .Where(element => element.BoundingRectangle.Left < Window.BoundingRectangle.Left + SidebarWidthEstimate)
                    .Where(element => names.Any(name => string.Equals(element.Name, name, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(element => element.BoundingRectangle.Top)
                    .ToList();
                if (candidates.Count > 0)
                {
                    return candidates[0];
                }
            }
            catch
            {
            }

            Thread.Sleep(100);
        }

        return null;
    }

    private AutomationElement? TryFindSidebarButton(IEnumerable<string> texts, int timeoutMs)
    {
        var names = texts.Where(text => !string.IsNullOrWhiteSpace(text)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var buttons = GetSidebarButtons(timeoutMs);
        var named = buttons.FirstOrDefault(button => names.Any(name => string.Equals(button.Name, name, StringComparison.OrdinalIgnoreCase)));
        return named ?? buttons.FirstOrDefault();
    }

    private List<AutomationElement> GetSidebarButtons(int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var buttons = Window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                    .Where(element => element.BoundingRectangle.Left < Window.BoundingRectangle.Left + SidebarWidthEstimate)
                    .Where(element => element.BoundingRectangle.Top > Window.BoundingRectangle.Top + 180)
                    .Where(element => element.BoundingRectangle.Width >= 90)
                    .OrderBy(element => element.BoundingRectangle.Top)
                    .ToList();
                if (buttons.Count > 0)
                {
                    return buttons;
                }
            }
            catch
            {
            }

            Thread.Sleep(100);
        }

        return [];
    }

    private void ConfirmLogoutIfNeeded()
    {
        var dialog = Driver.TryFindWindowByTitle("Logout", 2000);
        if (dialog is null)
        {
            return;
        }

        var buttons = dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
            .Select(element => element.AsButton())
            .OrderBy(button => button.BoundingRectangle.Left)
            .ToList();

        var confirm = buttons.FirstOrDefault(button => button.Name.Contains("Yes", StringComparison.OrdinalIgnoreCase)
            || button.Name.Contains("Có", StringComparison.OrdinalIgnoreCase)
            || button.Name.Contains("OK", StringComparison.OrdinalIgnoreCase))
            ?? (buttons.Count > 0 ? buttons[0] : null);
        if (confirm is not null)
        {
            ClickElement(confirm);
        }
    }

    private void TryClickLogout()
    {
        var buttons = GetSidebarButtons(400);
        if (buttons.Count > 0)
        {
            ClickElement(buttons[^1]);
            return;
        }

        if (GetSidebarAnchor() is not { } anchor)
        {
            return;
        }

        var windowBottom = (int)Math.Round(Convert.ToDouble(Window.BoundingRectangle.Bottom));
        Driver.LeftClickScreen(anchor.X, windowBottom - 44);
    }

    private void TryClickSidebarRow(int rowIndex, Func<bool>? successCheck = null)
    {
        var buttons = GetSidebarButtons(400);
        if (rowIndex >= 0 && rowIndex < buttons.Count)
        {
            ClickElement(buttons[rowIndex]);
            if (successCheck is null || WaitForSuccess(successCheck, 1200))
            {
                return;
            }
        }

        if (GetSidebarAnchor() is not { } anchor)
        {
            return;
        }

        var xOffsets = new[] { 0, -28, 28, -48, 48 };
        var yOffsets = new[] { 0, -12, 12, -20, 20 };
        var targetYs = new[]
        {
            anchor.Y + (rowIndex * SidebarNavRowStep),
            anchor.Y + ((rowIndex + 1) * SidebarNavRowStep),
        };

        foreach (var targetY in targetYs)
        {
            foreach (var yOffset in yOffsets)
            {
                foreach (var xOffset in xOffsets)
                {
                    Driver.LeftClickScreen(anchor.X + xOffset, targetY + yOffset);
                    if (successCheck is null || successCheck())
                    {
                        return;
                    }
                }
            }
        }
    }

    private Point? GetSidebarAnchor()
    {
        try
        {
            var sidebarCombo = Window.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox))
                .Where(element => element.BoundingRectangle.Left < Window.BoundingRectangle.Left + SidebarWidthEstimate)
                .OrderBy(element => element.BoundingRectangle.Top)
                .FirstOrDefault();

            if (sidebarCombo is not null)
            {
                var x = (int)Math.Round(Convert.ToDouble(sidebarCombo.BoundingRectangle.Left) + SidebarButtonCenterOffsetX);
                var y = (int)Math.Round(Convert.ToDouble(sidebarCombo.BoundingRectangle.Bottom) + SidebarNavTopOffset);
                return new Point(x, y);
            }
        }
        catch
        {
        }

        var fallbackX = (int)Math.Round(Convert.ToDouble(Window.BoundingRectangle.Left) + 92d);
        var fallbackY = (int)Math.Round(Convert.ToDouble(Window.BoundingRectangle.Top) + 208d);
        return new Point(fallbackX, fallbackY);
    }

    private void ClickElement(AutomationElement element)
    {
        var invoke = element.Patterns.Invoke.PatternOrDefault;
        if (invoke is not null)
        {
            try
            {
                invoke.Invoke();
                return;
            }
            catch
            {
            }
        }

        var x = (int)Math.Round(Convert.ToDouble(element.BoundingRectangle.Left + (element.BoundingRectangle.Width / 2)));
        var y = (int)Math.Round(Convert.ToDouble(element.BoundingRectangle.Top + (element.BoundingRectangle.Height / 2)));
        Driver.LeftClickScreen(x, y);
    }
}

