using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using JiraClone.E2ETests.Infrastructure;

namespace JiraClone.E2ETests.Pages;

public sealed class SprintManagementPage : PageBase
{
    public SprintManagementPage(Window window, AppDriver driver) : base(window, driver)
    {
    }

    public AutomationElement SprintList =>
        TryFind("SprintMgmt_ListView_Sprints", 1500)
        ?? Driver.TryFindText(Window, "Sprint", 1500)
        ?? Window;

    public bool IsCreateEnabled() => FindActionButton(0)?.IsEnabled ?? false;

    public bool IsAssignEnabled() => FindActionButton(1)?.IsEnabled ?? false;

    public bool IsStartEnabled() => FindActionButton(2)?.IsEnabled ?? false;

    public bool IsCloseEnabled() => FindActionButton(3)?.IsEnabled ?? false;

    public Window ClickCreate()
    {
        var button = FindActionButton(0) ?? throw new TimeoutException("Timed out waiting for the create-sprint button.");
        ClickButton(button);
        return Driver.WaitForWindowContainingElement("CreateSprint_TextBox_Name", Driver.Config.ActionTimeoutMs);
    }

    public void ClickAssign() => ClickButton(FindActionButton(1) ?? throw new TimeoutException("Timed out waiting for the assign button."));

    public void ClickStart() => ClickButton(FindActionButton(2) ?? throw new TimeoutException("Timed out waiting for the start button."));

    public void ClickClose() => ClickButton(FindActionButton(3) ?? throw new TimeoutException("Timed out waiting for the close button."));

    public bool ContainsText(string text, int timeoutMs = 3000) => Driver.TryFindText(Window, text, timeoutMs) is not null;

    private FlaUI.Core.AutomationElements.Button? FindActionButton(int index)
    {
        var direct = index switch
        {
            0 => TryFind("SprintMgmt_Button_Create", 500)?.AsButton(),
            1 => TryFind("SprintMgmt_Button_Assign", 500)?.AsButton(),
            2 => TryFind("SprintMgmt_Button_Start", 500)?.AsButton(),
            3 => TryFind("SprintMgmt_Button_Close", 500)?.AsButton(),
            _ => null,
        };
        if (direct is not null)
        {
            return direct;
        }

        var buttons = Window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
            .Select(element => element.AsButton())
            .Where(button => button.BoundingRectangle.Top < Window.BoundingRectangle.Top + 260)
            .OrderBy(button => button.BoundingRectangle.Left)
            .ToList();

        return index >= 0 && index < buttons.Count ? buttons[index] : null;
    }

    private void ClickButton(FlaUI.Core.AutomationElements.Button button)
    {
        var x = (int)Math.Round(Convert.ToDouble(button.BoundingRectangle.Left + (button.BoundingRectangle.Width / 2)));
        var y = (int)Math.Round(Convert.ToDouble(button.BoundingRectangle.Top + (button.BoundingRectangle.Height / 2)));
        Driver.LeftClickScreen(x, y);
    }
}
