using FlaUI.Core.AutomationElements;
using JiraClone.E2ETests.Infrastructure;
using JiraClone.E2ETests.Pages;

namespace JiraClone.E2ETests.Scenarios;

public sealed class AdminScenarioTests : E2ETestBase
{
    public AdminScenarioTests(AppDriver driver) : base(driver)
    {
    }

    [Fact]
    public void Admin_Login_Success()
    {
        RunWithFailureScreenshot(() =>
        {
            LoginAsAdmin();
            Assert.NotNull(Driver.WaitForMainWindow());
            Assert.NotNull(WaitForElement("MainForm_TextBox_Search"));
        });
    }

    [Fact]
    public void Admin_NavigateTo_UserManagement()
    {
        RunWithFailureScreenshot(() =>
        {
            var mainPage = LoginAsAdmin();
            var usersPage = mainPage.OpenUsers();
            usersPage.SelectFirstUser();

            Assert.True(usersPage.IsCreateVisibleAndEnabled());
            Assert.True(usersPage.IsEditVisibleAndEnabled());
            Assert.True(usersPage.IsResetPasswordVisibleAndEnabled());
        });
    }

    [Fact]
    public void Admin_CreateNewUser()
    {
        RunWithFailureScreenshot(() =>
        {
            var suffix = UniqueSuffix();
            var username = $"e2e_admin_{suffix}";
            var displayName = $"E2E Admin {suffix}";
            var email = $"e2e_admin_{suffix}@jiraclone.local";

            var mainPage = LoginAsAdmin();
            var usersPage = mainPage.OpenUsers();
            var dialog = usersPage.ClickCreate();

            Driver.WaitForElement(dialog, "UserEditor_TextBox_UserName").AsTextBox().Enter(username);
            Driver.WaitForElement(dialog, "UserEditor_TextBox_DisplayName").AsTextBox().Enter(displayName);
            Driver.WaitForElement(dialog, "UserEditor_TextBox_Email").AsTextBox().Enter(email);
            Driver.WaitForElement(dialog, "UserEditor_TextBox_Password").AsTextBox().Enter("ChangeMe123!");

            var roles = Driver.WaitForElement(dialog, "UserEditor_CheckedListBox_Roles");
            var developerRole = Driver.TryFindText(roles, "Developer", 3000)
                ?? throw new InvalidOperationException("Developer role option was not found.");
            developerRole.Click();

            Driver.WaitForElement(dialog, "UserEditor_Button_Save").AsButton().Click();

            Assert.True(SpinWait.SpinUntil(() => Driver.TryFindWindowContainingElement("UserEditor_TextBox_UserName", 100) is null, TimeSpan.FromSeconds(5)));

            usersPage.Search(username);
            Assert.True(SpinWait.SpinUntil(() => usersPage.ContainsText(username, 250), TimeSpan.FromSeconds(5)));
        });
    }

    [Fact]
    public void Admin_Logout()
    {
        RunWithFailureScreenshot(() =>
        {
            var mainPage = LoginAsAdmin();
            var loginPage = mainPage.Logout();
            Assert.True(loginPage.IsVisible());
        });
    }
}
