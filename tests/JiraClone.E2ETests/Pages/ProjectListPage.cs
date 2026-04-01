using FlaUI.Core.AutomationElements;
using JiraClone.E2ETests.Infrastructure;

namespace JiraClone.E2ETests.Pages;

public sealed class ProjectListPage : PageBase
{
    public ProjectListPage(Window window, AppDriver driver) : base(window, driver)
    {
    }

    public CreateProjectPage ClickCreateProject()
    {
        FindButton("ProjectList_Button_CreateProject").Click();
        return new CreateProjectPage(Driver.WaitForWindowByTitle("Create Project", Driver.Config.ActionTimeoutMs), Driver);
    }

    public void ClickOpenProject() => FindButton("ProjectList_Button_OpenProject").Click();

    public void SwitchToCardsView() => FindButton("ProjectList_Button_CardsView").Click();

    public void SwitchToGridView() => FindButton("ProjectList_Button_GridView").Click();

    public AutomationElement Grid => Find("ProjectList_ListView_Grid");
}
