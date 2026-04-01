using FlaUI.Core.AutomationElements;
using JiraClone.E2ETests.Infrastructure;

namespace JiraClone.E2ETests.Pages;

public sealed class IssueEditorPage : PageBase
{
    public IssueEditorPage(Window window, AppDriver driver) : base(window, driver)
    {
    }

    public void EnterTitle(string title) => FindTextBox("IssueEditor_TextBox_Title").Enter(title);

    public void EnterDescription(string description) => FindTextBox("IssueEditor_TextBox_Description").Enter(description);

    public void SelectType(string type) => FindComboBox("IssueEditor_ComboBox_Type").Select(type);

    public void SelectStatus(string status) => FindComboBox("IssueEditor_ComboBox_Status").Select(status);

    public void SelectPriority(string priority) => FindComboBox("IssueEditor_ComboBox_Priority").Select(priority);

    public void SelectReporter(string reporter) => FindComboBox("IssueEditor_ComboBox_Reporter").Select(reporter);

    public AutomationElement Assignees => Find("IssueEditor_CheckedListBox_Assignees");

    public AutomationElement DueDate => Find("IssueEditor_DatePicker_DueDate");

    public void ClickSave() => FindButton("IssueEditor_Button_Save").Click();

    public void ClickCancel() => FindButton("IssueEditor_Button_Cancel").Click();

    public bool IsClosed(int timeoutMs = 1500) => Driver.TryFindWindowContainingElement("IssueEditor_TextBox_Title", timeoutMs) is null;
}
