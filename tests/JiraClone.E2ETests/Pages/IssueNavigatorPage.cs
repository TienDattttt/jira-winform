using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using JiraClone.E2ETests.Infrastructure;

namespace JiraClone.E2ETests.Pages;

public sealed class IssueNavigatorPage : PageBase
{
    public IssueNavigatorPage(Window window, AppDriver driver) : base(window, driver)
    {
    }

    public void SelectStatus(string status) => FindComboBox("IssueNav_ComboBox_Status").Select(status);

    public void SelectPriority(string priority) => FindComboBox("IssueNav_ComboBox_Priority").Select(priority);

    public void SelectType(string type) => FindComboBox("IssueNav_ComboBox_Type").Select(type);

    public void Search(string text) => FindTextBox("IssueNav_TextBox_Search").Enter(text);

    public IssueEditorPage ClickCreateIssue()
    {
        var button = TryFind("IssueNav_Button_CreateIssue", 1000)
            ?? TryFindText("T?o issue", 500)
            ?? TryFindText("Create issue", 500)
            ?? FindCreateIssueButton()
            ?? throw new TimeoutException("Timed out waiting for the create-issue button.");

        ClickElement(button);
        var dialog = Driver.WaitForWindowContainingElement("IssueEditor_TextBox_Title", Driver.Config.ActionTimeoutMs);
        return new IssueEditorPage(dialog, Driver);
    }

    public void ClickOpen()
    {
        var button = TryFind("IssueNav_Button_Open", 1000)
            ?? Window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                .FirstOrDefault(candidate => candidate.Name.Contains("Open", StringComparison.OrdinalIgnoreCase));
        ClickElement(button ?? throw new TimeoutException("Timed out waiting for the open-issue button."));
    }

    public AutomationElement Results =>
        TryFind("IssueNav_DataGridView_Results", 1500)
        ?? Driver.TryFindText(Window, "Summary", 1500)
        ?? Window;

    public bool ContainsText(string text, int timeoutMs = 3000) => Driver.TryFindText(Results, text, timeoutMs) is not null;

    private AutomationElement? FindCreateIssueButton()
    {
        var buttons = Window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
            .Where(button => button.BoundingRectangle.Top < Window.BoundingRectangle.Top + 260)
            .OrderByDescending(button => button.BoundingRectangle.Left)
            .ThenBy(button => button.BoundingRectangle.Top)
            .ToList();

        return buttons.FirstOrDefault(button => button.Name.Contains("T?o", StringComparison.OrdinalIgnoreCase)
            || button.Name.Contains("Create", StringComparison.OrdinalIgnoreCase))
            ?? buttons.FirstOrDefault(button => button.BoundingRectangle.Width >= 100)
            ?? buttons.FirstOrDefault();
    }

    private void ClickElement(AutomationElement element)
    {
        if (element.BoundingRectangle.Width > 0 && element.BoundingRectangle.Height > 0)
        {
            var x = (int)Math.Round(Convert.ToDouble(element.BoundingRectangle.Left + (element.BoundingRectangle.Width / 2)));
            var y = (int)Math.Round(Convert.ToDouble(element.BoundingRectangle.Top + (element.BoundingRectangle.Height / 2)));
            Driver.LeftClickScreen(x, y);
            return;
        }

        var invoke = element.Patterns.Invoke.PatternOrDefault;
        if (invoke is not null)
        {
            invoke.Invoke();
        }
    }
}

