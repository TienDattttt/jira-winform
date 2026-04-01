using FlaUI.Core.AutomationElements;
using JiraClone.E2ETests.Infrastructure;

namespace JiraClone.E2ETests.Scenarios;

public sealed class ProjectManagerScenarioTests : E2ETestBase
{
    public ProjectManagerScenarioTests(AppDriver driver) : base(driver)
    {
    }

    [Fact]
    public void PM_Login_Success()
    {
        RunWithFailureScreenshot(() =>
        {
            var mainPage = LoginAsProjectManager();
            Assert.NotNull(Driver.WaitForMainWindow());
            Assert.False(mainPage.HasUsersMenu());
        });
    }

    [Fact]
    public void PM_CreateSprint()
    {
        RunWithFailureScreenshot(() =>
        {
            var sprintName = $"E2E Sprint {UniqueSuffix()}";
            var mainPage = LoginAsProjectManager();
            var sprintPage = mainPage.OpenSprints();

            Assert.True(sprintPage.IsCreateEnabled());

            var dialog = sprintPage.ClickCreate();
            Driver.WaitForElement(dialog, "CreateSprint_TextBox_Name").AsTextBox().Enter(sprintName);
            Driver.WaitForElement(dialog, "CreateSprint_TextBox_Goal").AsTextBox().Enter("Created from E2E test");
            Driver.WaitForElement(dialog, "CreateSprint_Button_Save").AsButton().Click();

            Assert.True(SpinWait.SpinUntil(() => Driver.TryFindWindowContainingElement("CreateSprint_TextBox_Name", 100) is null, TimeSpan.FromSeconds(5)));
            Assert.True(SpinWait.SpinUntil(() => sprintPage.ContainsText(sprintName, 250), TimeSpan.FromSeconds(5)));
        });
    }

    [Fact]
    public void PM_CreateIssue()
    {
        RunWithFailureScreenshot(() =>
        {
            var issueTitle = $"E2E PM Issue {UniqueSuffix()}";
            var mainPage = LoginAsProjectManager();
            var issuePage = mainPage.OpenIssues();
            var dialog = issuePage.ClickCreateIssue();

            dialog.EnterTitle(issueTitle);
            dialog.SelectType("Task");
            dialog.SelectPriority("Medium");
            dialog.ClickSave();

            Assert.True(SpinWait.SpinUntil(() => Driver.TryFindWindowContainingElement("IssueEditor_TextBox_Title", 100) is null, TimeSpan.FromSeconds(5)));
        });
    }

    [Fact]
    public void PM_CannotAccess_UserManagement()
    {
        RunWithFailureScreenshot(() =>
        {
            var mainPage = LoginAsProjectManager();
            Assert.False(mainPage.HasUsersMenu());
        });
    }
}
