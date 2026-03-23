using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public sealed class GeneratedApiTokenDialog : Form
{
    public GeneratedApiTokenDialog(string rawToken)
    {
        Text = "API Token Created";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Width = 640;
        Height = 320;
        MinimumSize = new Size(640, 320);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;

        var warning = JiraControlFactory.CreateLabel("Copy this token now. You won't be able to see it again after closing this dialog.", true);
        warning.ForeColor = JiraTheme.Red600;
        warning.MaximumSize = new Size(560, 0);
        warning.AutoSize = true;
        warning.Margin = new Padding(0, 0, 0, 10);

        var tokenBox = JiraControlFactory.CreateTextBox();
        tokenBox.Multiline = true;
        tokenBox.ReadOnly = true;
        tokenBox.ScrollBars = ScrollBars.Vertical;
        tokenBox.Width = 560;
        tokenBox.Height = 96;
        tokenBox.Text = rawToken;

        var copyButton = JiraControlFactory.CreatePrimaryButton("Copy Token");
        copyButton.Click += (_, _) =>
        {
            Clipboard.SetText(rawToken);
            MessageBox.Show(this, "API token copied to clipboard.", "Copy Token", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var closeButton = JiraControlFactory.CreateSecondaryButton("Close");
        closeButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        var buttonRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            BackColor = JiraTheme.BgSurface,
            Margin = new Padding(0, 10, 0, 0),
        };
        buttonRow.Controls.Add(copyButton);
        buttonRow.Controls.Add(closeButton);

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(24),
            BackColor = JiraTheme.BgSurface,
        };
        layout.Controls.Add(JiraControlFactory.CreateLabel("Your new API token", true));
        layout.Controls.Add(warning);
        layout.Controls.Add(tokenBox);
        layout.Controls.Add(buttonRow);

        Controls.Add(layout);
        AcceptButton = closeButton;
    }
}

