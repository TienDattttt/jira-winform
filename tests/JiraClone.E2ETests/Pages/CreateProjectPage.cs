using FlaUI.Core.AutomationElements;
using JiraClone.E2ETests.Infrastructure;

namespace JiraClone.E2ETests.Pages;

public sealed class CreateProjectPage : PageBase
{
    public CreateProjectPage(Window window, AppDriver driver) : base(window, driver)
    {
    }

    public void EnterName(string name) => FindTextBox("CreateProject_TextBox_Name").Enter(name);

    public void EnterKey(string key) => FindTextBox("CreateProject_TextBox_Key").Enter(key);

    public void SelectCategory(string category) => FindComboBox("CreateProject_ComboBox_Category").Select(category);

    public void EnterDescription(string description) => FindTextBox("CreateProject_TextBox_Description").Enter(description);

    public void ClickNext() => FindButton("CreateProject_Button_Next").Click();

    public void ClickCreate() => FindButton("CreateProject_Button_Create").Click();

    public void ClickCancel() => FindButton("CreateProject_Button_Cancel").Click();
}
