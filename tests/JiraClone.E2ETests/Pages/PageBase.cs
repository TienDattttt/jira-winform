using FlaUI.Core.AutomationElements;
using JiraClone.E2ETests.Infrastructure;

namespace JiraClone.E2ETests.Pages;

public abstract class PageBase
{
    protected PageBase(Window window, AppDriver driver)
    {
        Window = window;
        Driver = driver;
    }

    protected Window Window { get; }
    protected AppDriver Driver { get; }

    protected AutomationElement Find(string automationId, int timeoutMs = 5000) =>
        Driver.WaitForElement(Window, automationId, timeoutMs);

    protected AutomationElement? TryFind(string automationId, int timeoutMs = 1500) =>
        Driver.TryFindElement(Window, automationId, timeoutMs);

    protected AutomationElement? TryFindText(string text, int timeoutMs = 1500) =>
        Driver.TryFindText(Window, text, timeoutMs);

    protected FlaUI.Core.AutomationElements.Button FindButton(string automationId, int timeoutMs = 5000) =>
        Find(automationId, timeoutMs).AsButton();

    protected FlaUI.Core.AutomationElements.TextBox FindTextBox(string automationId, int timeoutMs = 5000) =>
        Find(automationId, timeoutMs).AsTextBox();

    protected FlaUI.Core.AutomationElements.ComboBox FindComboBox(string automationId, int timeoutMs = 5000) =>
        Find(automationId, timeoutMs).AsComboBox();
}
