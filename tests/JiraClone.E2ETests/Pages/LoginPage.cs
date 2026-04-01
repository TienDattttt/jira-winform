using FlaUI.Core.AutomationElements;
using JiraClone.E2ETests.Infrastructure;

namespace JiraClone.E2ETests.Pages;

public sealed class LoginPage : PageBase
{
    public LoginPage(Window window, AppDriver driver) : base(window, driver)
    {
    }

    public void EnterEmail(string email) => FindTextBox("LoginForm_TextBox_Email").Enter(email);

    public void EnterPassword(string password) => FindTextBox("LoginForm_TextBox_Password").Enter(password);

    public void SetRememberMe(bool rememberMe)
    {
        var checkBox = Find("LoginForm_CheckBox_RememberMe").AsCheckBox();
        if ((checkBox.IsChecked ?? false) != rememberMe)
        {
            checkBox.Click();
        }
    }

    public void ToggleShowPassword() => FindButton("LoginForm_Button_ShowPassword").Click();

    public MainPage ClickLogin()
    {
        var button = FindButton("LoginForm_Button_Login");
        ClickElement(button);

        var mainWindow = Driver.TryFindWindow(window =>
            window.Title.Contains("Jira Clone Desktop", StringComparison.OrdinalIgnoreCase)
            || Driver.TryFindElement(window, "MainForm_TextBox_Search", 50) is not null,
            Driver.Config.ActionTimeoutMs);

        if (mainWindow is null)
        {
            ClickElement(button);
            mainWindow = Driver.WaitForMainWindow();
        }

        return new MainPage(mainWindow, Driver);
    }

    public void Close() => FindButton("LoginForm_Button_Close").Click();

    public bool IsVisible() =>
        Driver.TryFindElement(Window, "LoginForm_TextBox_Email", 500) is not null
        && Driver.TryFindElement(Window, "LoginForm_Button_Login", 500) is not null;

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
