using FlaUI.Core.AutomationElements;
using JiraClone.E2ETests.Infrastructure;

namespace JiraClone.E2ETests.Pages;

public sealed class UserManagementPage : PageBase
{
    public UserManagementPage(Window window, AppDriver driver) : base(window, driver)
    {
    }

    public AutomationElement Users =>
        TryFind("UserMgmt_ListView_Users", 1500)
        ?? Driver.TryFindText(Window, "Display Name", 1500)
        ?? Window;

    public bool IsCreateVisibleAndEnabled() => IsEnabled("UserMgmt_Button_Create", "Create", "T?o");

    public bool IsEditVisibleAndEnabled() => IsEnabled("UserMgmt_Button_Edit", "Edit", "S?a");

    public bool IsResetPasswordVisibleAndEnabled() => IsEnabled("UserMgmt_Button_ResetPassword", "Reset Password", "Ð?t l?i m?t kh?u");

    public void Search(string text) => FindTextBox("UserMgmt_TextBox_Search").Enter(text);

    public void SelectStatus(string status) => FindComboBox("UserMgmt_ComboBox_StatusFilter").Select(status);

    public void SelectFirstUser(int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (TrySelectKnownUserByText() && WaitForSelection(800))
            {
                return;
            }

            if (TrySelectFirstRowByCoordinates() && WaitForSelection(800))
            {
                return;
            }

            Thread.Sleep(150);
        }

        throw new InvalidOperationException("No user row was selected.");
    }

    public bool ContainsText(string text, int timeoutMs = 3000) => Driver.TryFindText(Window, text, timeoutMs) is not null;

    public Window ClickCreate()
    {
        var button = TryFind("UserMgmt_Button_Create", 1500)
            ?? TryFindText("Create", 1000)
            ?? TryFindText("T?o", 1000)
            ?? throw new TimeoutException("Timed out waiting for the create-user button.");
        ClickElement(button);
        return Driver.WaitForWindowContainingElement("UserEditor_TextBox_UserName", Driver.Config.ActionTimeoutMs);
    }

    public void ClickEdit() => ClickButton("UserMgmt_Button_Edit", "Edit", "S?a");

    public void ClickDeactivate() => ClickButton("UserMgmt_Button_Deactivate", "Deactivate", "Vô hi?u hóa");

    public void ClickActivate() => ClickButton("UserMgmt_Button_Activate", "Activate", "Kích ho?t");

    public void ClickResetPassword() => ClickButton("UserMgmt_Button_ResetPassword", "Reset Password", "Ð?t l?i m?t kh?u");

    private bool TrySelectKnownUserByText()
    {
        var candidate = Driver.TryFindText(Window, "Admin User", 250)
            ?? Driver.TryFindText(Window, "Gaben", 250)
            ?? Driver.TryFindText(Window, "Yoda", 250);
        if (candidate is null)
        {
            return false;
        }

        ClickElement(candidate);
        return true;
    }

    private bool TrySelectFirstRowByCoordinates()
    {
        var list = Users;
        var bounds = list.BoundingRectangle;
        if (bounds.Width < 120 || bounds.Height < 80)
        {
            return false;
        }

        var x = (int)Math.Round(Convert.ToDouble(bounds.Left + Math.Min(120d, bounds.Width * 0.2)));
        var y = (int)Math.Round(Convert.ToDouble(bounds.Top + 54d));
        Driver.LeftClickScreen(x, y);
        return true;
    }

    private bool WaitForSelection(int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (IsEditVisibleAndEnabled() && IsResetPasswordVisibleAndEnabled())
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return false;
    }

    private bool IsEnabled(string automationId, params string[] fallbackTexts)
    {
        var element = TryFind(automationId, 1200);
        if (element is null)
        {
            element = fallbackTexts.Select(text => TryFindText(text, 1200)).FirstOrDefault(candidate => candidate is not null);
        }

        return element?.IsEnabled ?? false;
    }

    private void ClickButton(string automationId, params string[] fallbackTexts)
    {
        var element = TryFind(automationId, 1200);
        if (element is null)
        {
            element = fallbackTexts.Select(text => TryFindText(text, 1200)).FirstOrDefault(candidate => candidate is not null);
        }

        ClickElement(element ?? throw new TimeoutException($"Timed out waiting for button '{automationId}'."));
    }

    private void ClickElement(AutomationElement element)
    {
        var invoke = element.Patterns.Invoke.PatternOrDefault;
        if (invoke is not null)
        {
            invoke.Invoke();
            return;
        }

        var x = (int)Math.Round(Convert.ToDouble(element.BoundingRectangle.Left + (element.BoundingRectangle.Width / 2)));
        var y = (int)Math.Round(Convert.ToDouble(element.BoundingRectangle.Top + (element.BoundingRectangle.Height / 2)));
        Driver.LeftClickScreen(x, y);
    }
}
