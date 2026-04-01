using JiraClone.E2ETests.Infrastructure;

namespace JiraClone.E2ETests.Scenarios;

public sealed class DeveloperScenarioTests : E2ETestBase
{
    public DeveloperScenarioTests(AppDriver driver) : base(driver)
    {
    }

    [Fact]
    public void Dev_Login_Success()
    {
        RunWithFailureScreenshot(() =>
        {
            LoginAsDeveloper();
            Assert.NotNull(Driver.WaitForMainWindow());
        });
    }

    [Fact]
    public void Dev_CannotSee_UsersMenu()
    {
        RunWithFailureScreenshot(() =>
        {
            var mainPage = LoginAsDeveloper();
            Assert.False(mainPage.HasUsersMenu());
        });
    }

    [Fact]
    public void Dev_SprintButtons_Disabled()
    {
        RunWithFailureScreenshot(() =>
        {
            var mainPage = LoginAsDeveloper();
            var sprintPage = mainPage.OpenSprints();

            Assert.False(sprintPage.IsCreateEnabled());
            Assert.False(sprintPage.IsStartEnabled());
            Assert.False(sprintPage.IsCloseEnabled());
        });
    }

    [Fact]
    public void Dev_CanCreate_Issue()
    {
        RunWithFailureScreenshot(() =>
        {
            var issueTitle = $"E2E Dev Issue {UniqueSuffix()}";
            var mainPage = LoginAsDeveloper();
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
    public void Dev_Board_Opens_Successfully()
    {
        RunWithFailureScreenshot(() =>
        {
            var mainPage = LoginAsDeveloper();
            mainPage.OpenBoard();
            var window = Driver.WaitForMainWindow();

            Assert.True(SpinWait.SpinUntil(() => Driver.TryFindText(window, "Nhóm theo Epic", 250) is not null, TimeSpan.FromSeconds(5)));
        });
    }
}
