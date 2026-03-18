using JiraClone.Application.Integrations;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms.Integrations;

public sealed class GitHubIntegrationConfigDialog : Form
{
    private readonly TextBox _owner = JiraControlFactory.CreateTextBox();
    private readonly TextBox _repo = JiraControlFactory.CreateTextBox();
    private readonly TextBox _apiToken = JiraControlFactory.CreateTextBox();
    private readonly CheckBox _enabled = new() { Text = "Enabled", AutoSize = true, ForeColor = JiraTheme.TextPrimary, BackColor = JiraTheme.BgSurface, Font = JiraTheme.FontBody };
    private readonly Label _validation = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly Button _ok = JiraControlFactory.CreatePrimaryButton("Save");

    public GitHubIntegrationConfigDialog(GitHubProjectConfig? config = null, bool isEnabled = true)
    {
        Text = "Configure GitHub";
        Width = 500;
        Height = 360;
        MinimumSize = new Size(500, 360);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;

        _apiToken.UseSystemPasswordChar = true;
        _validation.ForeColor = JiraTheme.Red600;
        _validation.AutoSize = true;

        _owner.Text = config?.Owner ?? string.Empty;
        _repo.Text = config?.Repo ?? string.Empty;
        _apiToken.Text = config?.ApiToken ?? string.Empty;
        _enabled.Checked = isEnabled;

        _owner.TextChanged += (_, _) => ValidateInput();
        _repo.TextChanged += (_, _) => ValidateInput();
        _apiToken.TextChanged += (_, _) => ValidateInput();
        _enabled.CheckedChanged += (_, _) => ValidateInput();

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

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20), BackColor = JiraTheme.BgSurface, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(JiraControlFactory.CreateLabel("Owner", true), 0, 0);
        layout.Controls.Add(_owner, 1, 0);
        layout.Controls.Add(JiraControlFactory.CreateLabel("Repository", true), 0, 1);
        layout.Controls.Add(_repo, 1, 1);
        layout.Controls.Add(JiraControlFactory.CreateLabel("API token", true), 0, 2);
        layout.Controls.Add(_apiToken, 1, 2);
        layout.Controls.Add(_enabled, 1, 3);
        layout.Controls.Add(_validation, 1, 4);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = JiraTheme.BgSurface };
        buttons.Controls.Add(_ok);
        buttons.Controls.Add(cancel);

        Controls.Add(layout);
        Controls.Add(buttons);
        ValidateInput();
    }

    public GitHubProjectConfig Config => new(_owner.Text.Trim(), _repo.Text.Trim(), _apiToken.Text.Trim());
    public bool IsEnabled => _enabled.Checked;

    private bool ValidateInput()
    {
        string? error = null;
        if (string.IsNullOrWhiteSpace(_owner.Text)) error = "Owner is required.";
        else if (string.IsNullOrWhiteSpace(_repo.Text)) error = "Repository is required.";
        else if (string.IsNullOrWhiteSpace(_apiToken.Text)) error = "API token is required.";
        _validation.Text = error ?? string.Empty;
        _ok.Enabled = string.IsNullOrWhiteSpace(error);
        return string.IsNullOrWhiteSpace(error);
    }
}

