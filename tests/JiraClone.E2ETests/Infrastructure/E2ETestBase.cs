using System.Runtime.CompilerServices;
using FlaUI.Core.AutomationElements;
using JiraClone.E2ETests.Pages;

namespace JiraClone.E2ETests.Infrastructure;

public abstract class E2ETestBase : IClassFixture<AppDriver>, IDisposable
{
    protected E2ETestBase(AppDriver driver)
    {
        Driver = driver;
        Config = driver.Config;
    }

    protected AppDriver Driver { get; }
    protected TestConfig Config { get; }

    protected LoginPage StartAtLogin() => new(Driver.RestartToLogin(), Driver);

    protected MainPage LoginAs(string username, string password)
    {
        var page = StartAtLogin();
        page.EnterEmail(username);
        page.EnterPassword(password);
        return page.ClickLogin();
    }

    protected MainPage LoginAsAdmin() => LoginAs(Config.Admin.Username, Config.Admin.Password);

    protected MainPage LoginAsProjectManager() => LoginAs(Config.ProjectManager.Username, Config.ProjectManager.Password);

    protected MainPage LoginAsDeveloper() => LoginAs(Config.Developer.Username, Config.Developer.Password);

    protected void Logout()
    {
        var mainPage = new MainPage(Driver.WaitForMainWindow(), Driver);
        mainPage.Logout();
    }

    protected AutomationElement WaitForElement(string automationId, int timeoutMs = 5000)
    {
        var window = Driver.CurrentWindow ?? Driver.Launch();
        return Driver.WaitForElement(window, automationId, timeoutMs);
    }

    protected bool ElementExists(string automationId, int timeoutMs = 1500)
    {
        var window = Driver.CurrentWindow ?? Driver.Launch();
        return Driver.TryFindElement(window, automationId, timeoutMs) is not null;
    }

    protected bool HasErrorDialog(int timeoutMs = 1000) =>
        Driver.TryFindWindowByTitle("Unexpected Error", timeoutMs) is not null;

    protected static string UniqueSuffix() => DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");

    protected string CaptureScreenshot([CallerMemberName] string caller = "capture") =>
        Driver.CaptureWindow($"{DateTime.Now:yyyyMMdd-HHmmssfff}_{Sanitize(caller)}.png");

    protected void RunWithFailureScreenshot(Action action, [CallerMemberName] string caller = "scenario")
    {
        try
        {
            action();
        }
        catch
        {
            try
            {
                CaptureScreenshot($"failure_{caller}");
            }
            catch
            {
            }

            throw;
        }
    }

    public virtual void Dispose()
    {
        Driver.Quit();
    }

    private static string Sanitize(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }
}
