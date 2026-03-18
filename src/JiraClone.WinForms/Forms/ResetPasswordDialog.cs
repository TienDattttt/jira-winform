using JiraClone.Application.Auth;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public sealed class ResetPasswordDialog : Form
{
    private readonly TextBox _newPassword = JiraControlFactory.CreateTextBox();
    private readonly TextBox _confirmPassword = JiraControlFactory.CreateTextBox();
    private readonly Label _validationError = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly Button _okButton = JiraControlFactory.CreatePrimaryButton("Reset Password");
    private readonly Button _cancelButton = JiraControlFactory.CreateSecondaryButton("Cancel");

    public ResetPasswordDialog()
    {
        Text = "Reset Password";
        AutoScaleMode = AutoScaleMode.Font;
        Width = 520;
        Height = 320;
        MinimumSize = new Size(520, 320);
        MaximizeBox = false;
        MinimizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;
        Padding = new Padding(0);

        _newPassword.UseSystemPasswordChar = true;
        _confirmPassword.UseSystemPasswordChar = true;
        _newPassword.Width = 260;
        _confirmPassword.Width = 260;

        _validationError.ForeColor = JiraTheme.Red600;
        _validationError.Text = "Password must be at least 8 characters and include at least 1 uppercase letter and 1 number.";
        _validationError.MaximumSize = new Size(320, 0);
        _validationError.Visible = false;

        _okButton.AutoSize = false;
        _okButton.Width = 136;
        _okButton.Height = 40;
        _okButton.Enabled = false;
        _okButton.Click += (_, _) =>
        {
            if (_okButton.Enabled)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };

        _cancelButton.AutoSize = false;
        _cancelButton.Width = 100;
        _cancelButton.Height = 40;
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _newPassword.TextChanged += (_, _) => UpdateValidation();
        _confirmPassword.TextChanged += (_, _) => UpdateValidation();

        Controls.Add(BuildContent());
        Controls.Add(BuildButtons());

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
        UpdateValidation();
    }

    public string NewPassword => _newPassword.Text;

    private Control BuildContent()
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(20, 20, 20, 12),
            ColumnCount = 1,
            RowCount = 3,
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var title = JiraControlFactory.CreateLabel("Set a new password");
        title.Font = JiraTheme.FontH2;
        title.Margin = new Padding(0, 0, 0, 6);

        var caption = JiraControlFactory.CreateLabel("Use at least 8 characters with 1 uppercase letter and 1 number.", true);
        caption.Margin = new Padding(0, 0, 0, 14);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 3,
            BackColor = JiraTheme.BgSurface,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(layout, 0, "New Password", _newPassword);
        AddRow(layout, 1, "Confirm Password", _confirmPassword);
        layout.Controls.Add(_validationError, 1, 2);

        host.Controls.Add(title, 0, 0);
        host.Controls.Add(caption, 0, 1);
        host.Controls.Add(layout, 0, 2);

        return host;
    }

    private Control BuildButtons()
    {
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(12),
            Margin = new Padding(0),
        };
        buttons.Controls.Add(_okButton);
        buttons.Controls.Add(_cancelButton);
        return buttons;
    }

    private void UpdateValidation()
    {
        var error = AuthenticationService.GetPasswordValidationError(_newPassword.Text);
        if (error is null && !string.Equals(_newPassword.Text, _confirmPassword.Text, StringComparison.Ordinal))
        {
            error = "Passwords do not match.";
        }

        _validationError.Text = error ?? string.Empty;
        _validationError.Visible = !string.IsNullOrWhiteSpace(error);
        _okButton.Enabled = string.IsNullOrWhiteSpace(error);
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        var labelControl = JiraControlFactory.CreateLabel(label, true);
        labelControl.Margin = new Padding(0, 10, 8, 10);
        control.Margin = new Padding(0, 6, 0, 6);
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(labelControl, 0, row);
        layout.Controls.Add(control, 1, row);
    }
}

