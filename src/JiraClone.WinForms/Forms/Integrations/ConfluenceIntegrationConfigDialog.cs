using JiraClone.Application.Integrations;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms.Integrations;

public sealed class ConfluenceIntegrationConfigDialog : Form
{
    private readonly TextBox _baseUrl = JiraControlFactory.CreateTextBox();
    private readonly TextBox _spaceKey = JiraControlFactory.CreateTextBox();
    private readonly TextBox _email = JiraControlFactory.CreateTextBox();
    private readonly TextBox _apiToken = JiraControlFactory.CreateTextBox();
    private readonly CheckBox _enabled = new() { Text = "Enabled", AutoSize = true, ForeColor = JiraTheme.TextPrimary, BackColor = JiraTheme.BgSurface, Font = JiraTheme.FontBody };
    private readonly Label _validation = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly Button _ok = JiraControlFactory.CreatePrimaryButton("Save");

    public ConfluenceIntegrationConfigDialog(ConfluenceProjectConfig? config = null, bool isEnabled = true)
    {
        Text = "Configure Confluence";
        Width = 540;
        Height = 400;
        MinimumSize = new Size(540, 400);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;

        _apiToken.UseSystemPasswordChar = true;
        _validation.ForeColor = JiraTheme.Red600;
        _validation.AutoSize = true;

        _baseUrl.Text = config?.BaseUrl ?? string.Empty;
        _spaceKey.Text = config?.SpaceKey ?? string.Empty;
        _email.Text = config?.Email ?? string.Empty;
        _apiToken.Text = config?.ApiToken ?? string.Empty;
        _enabled.Checked = isEnabled;

        _baseUrl.TextChanged += (_, _) => ValidateInput();
        _spaceKey.TextChanged += (_, _) => ValidateInput();
        _email.TextChanged += (_, _) => ValidateInput();
        _apiToken.TextChanged += (_, _) => ValidateInput();

        _ok.Click += (_, _) =>
        {
            if (!ValidateInput())
            {
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };

        var cancel = JiraControlFactory.CreateSecondaryButton("Cancel");
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20), BackColor = JiraTheme.BgSurface, RowCount = 6 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(JiraControlFactory.CreateLabel("Base URL", true), 0, 0);
        layout.Controls.Add(_baseUrl, 1, 0);
        layout.Controls.Add(JiraControlFactory.CreateLabel("Space key", true), 0, 1);
        layout.Controls.Add(_spaceKey, 1, 1);
        layout.Controls.Add(JiraControlFactory.CreateLabel("Email", true), 0, 2);
        layout.Controls.Add(_email, 1, 2);
        layout.Controls.Add(JiraControlFactory.CreateLabel("API token", true), 0, 3);
        layout.Controls.Add(_apiToken, 1, 3);
        layout.Controls.Add(_enabled, 1, 4);
        layout.Controls.Add(_validation, 1, 5);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = JiraTheme.BgSurface };
        buttons.Controls.Add(_ok);
        buttons.Controls.Add(cancel);

        Controls.Add(layout);
        Controls.Add(buttons);
        ValidateInput();
    }

    public ConfluenceProjectConfig Config => new(_baseUrl.Text.Trim(), _spaceKey.Text.Trim(), _apiToken.Text.Trim(), _email.Text.Trim());
    public bool IsEnabled => _enabled.Checked;

    private bool ValidateInput()
    {
        string? error = null;
        if (!Uri.TryCreate(_baseUrl.Text.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) error = "Base URL must be a valid http or https URL.";
        else if (string.IsNullOrWhiteSpace(_spaceKey.Text)) error = "Space key is required.";
        else if (string.IsNullOrWhiteSpace(_email.Text)) error = "Email is required.";
        else if (string.IsNullOrWhiteSpace(_apiToken.Text)) error = "API token is required.";
        _validation.Text = error ?? string.Empty;
        _ok.Enabled = string.IsNullOrWhiteSpace(error);
        return string.IsNullOrWhiteSpace(error);
    }
}

