using System.Drawing;
using JiraClone.WinForms.Helpers;

namespace JiraClone.WinForms.Theme;

public static class JiraControlFactory
{
    public static Button CreatePrimaryButton(string text)
    {
        var button = CreateBaseButton(text);
        button.BackColor = JiraTheme.Primary;
        button.ForeColor = Color.White;
        button.FlatAppearance.BorderColor = JiraTheme.Primary;
        button.FlatAppearance.MouseOverBackColor = JiraTheme.PrimaryHover;
        button.FlatAppearance.MouseDownBackColor = JiraTheme.PrimaryActive;

        button.MouseEnter += (_, _) => button.BackColor = JiraTheme.PrimaryHover;
        button.MouseLeave += (_, _) => button.BackColor = JiraTheme.Primary;
        button.Resize += (_, _) => ApplyRoundedRegion(button);
        ApplyRoundedRegion(button);

        return button;
    }

    public static Button CreateSecondaryButton(string text)
    {
        var button = CreateBaseButton(text);
        button.BackColor = JiraTheme.BgSurface;
        button.ForeColor = JiraTheme.TextPrimary;
        button.FlatAppearance.BorderColor = JiraTheme.Border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = JiraTheme.Neutral100;
        button.FlatAppearance.MouseDownBackColor = JiraTheme.Neutral200;

        button.MouseEnter += (_, _) => button.BackColor = JiraTheme.Neutral100;
        button.MouseLeave += (_, _) => button.BackColor = JiraTheme.BgSurface;
        button.Resize += (_, _) => ApplyRoundedRegion(button);
        ApplyRoundedRegion(button);

        return button;
    }

    public static TextBox CreateTextBox()
    {
        var textBox = new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            Font = JiraTheme.FontBody,
            BackColor = JiraTheme.BgSurface,
            ForeColor = JiraTheme.TextPrimary,
            Margin = new Padding(JiraTheme.Padding),
        };

        textBox.Enter += (_, _) => textBox.BackColor = JiraTheme.Blue100;
        textBox.Leave += (_, _) => textBox.BackColor = JiraTheme.BgSurface;

        return textBox;
    }

    public static Label CreateLabel(string text, bool isCaption = false)
    {
        return new Label
        {
            AutoSize = true,
            Text = text,
            Font = isCaption ? JiraTheme.FontCaption : JiraTheme.FontBody,
            ForeColor = isCaption ? JiraTheme.TextSecondary : JiraTheme.TextPrimary,
            Margin = new Padding(JiraTheme.Padding, JiraTheme.Padding / 2, JiraTheme.Padding, JiraTheme.Padding / 2),
        };
    }

    public static Panel CreateSeparator()
    {
        return new Panel
        {
            Height = 1,
            Dock = DockStyle.Top,
            BackColor = JiraTheme.Border,
            Margin = new Padding(0, JiraTheme.Padding, 0, JiraTheme.Padding),
        };
    }

    private static Button CreateBaseButton(string text)
    {
        return new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlatStyle = FlatStyle.Flat,
            Font = JiraTheme.FontSmall,
            Cursor = Cursors.Hand,
            Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(JiraTheme.Padding),
            UseVisualStyleBackColor = false,
        };
    }

    private static void ApplyRoundedRegion(Control control)
    {
        if (control.Width <= 0 || control.Height <= 0)
        {
            return;
        }

        using var path = GraphicsHelper.CreateRoundedPath(new Rectangle(0, 0, control.Width, control.Height), JiraTheme.BorderRadius);
        control.Region = new Region(path);
    }


}



