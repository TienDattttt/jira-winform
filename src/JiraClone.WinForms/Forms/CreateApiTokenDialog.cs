using JiraClone.Domain.Enums;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public sealed class CreateApiTokenDialog : Form
{
    private readonly TextBox _name = JiraControlFactory.CreateTextBox();
    private readonly ComboBox _expiry = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Font = JiraTheme.FontBody,
        Width = 220,
        IntegralHeight = false,
    };
    private readonly CheckedListBox _scopes = new()
    {
        CheckOnClick = true,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = JiraTheme.BgSurface,
        ForeColor = JiraTheme.TextPrimary,
        Font = JiraTheme.FontBody,
        Height = 160,
        Width = 360,
    };
    private readonly Label _validation = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly Button _okButton = JiraControlFactory.CreatePrimaryButton("Create Token");

    public CreateApiTokenDialog()
    {
        Text = "Create API Token";
        AutoScaleMode = AutoScaleMode.Font;
        Width = 520;
        Height = 430;
        MinimumSize = new Size(520, 430);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;

        _validation.ForeColor = JiraTheme.Red600;
        _validation.MaximumSize = new Size(360, 0);
        _validation.AutoSize = true;

        _expiry.Items.AddRange(
        [
            new ExpiryOption("30 days", TimeSpan.FromDays(30)),
            new ExpiryOption("90 days", TimeSpan.FromDays(90)),
            new ExpiryOption("1 year", TimeSpan.FromDays(365)),
            new ExpiryOption("Never", null),
        ]);
        _expiry.SelectedIndex = 0;

        foreach (var scope in Enum.GetValues<ApiTokenScope>())
        {
            _scopes.Items.Add(scope, scope is ApiTokenScope.ReadIssues or ApiTokenScope.ReadProjects);
        }

        _name.Width = 360;
        _name.TextChanged += (_, _) => ValidateInput();
        _expiry.SelectedIndexChanged += (_, _) => ValidateInput();
        _scopes.ItemCheck += (_, _) => BeginInvoke(new Action(() => ValidateInput()));

        _okButton.Enabled = false;
        _okButton.Click += (_, _) =>
        {
            if (!ValidateInput())
            {
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };

        var cancelButton = JiraControlFactory.CreateSecondaryButton("Cancel");
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12),
            BackColor = JiraTheme.BgSurface,
        };
        buttons.Controls.Add(_okButton);
        buttons.Controls.Add(cancelButton);

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(16),
            BackColor = JiraTheme.BgSurface,
            AutoScroll = true,
        };
        layout.Controls.Add(JiraControlFactory.CreateLabel("Name", true));
        layout.Controls.Add(_name);
        layout.Controls.Add(JiraControlFactory.CreateLabel("Expiry", true));
        layout.Controls.Add(_expiry);
        layout.Controls.Add(JiraControlFactory.CreateLabel("Scopes", true));
        layout.Controls.Add(_scopes);
        layout.Controls.Add(_validation);

        Controls.Add(layout);
        Controls.Add(buttons);
        AcceptButton = _okButton;
        ValidateInput();
    }

    public string TokenName => _name.Text.Trim();
    public DateTime? ExpiresAtUtc => (_expiry.SelectedItem as ExpiryOption)?.CreateExpiryUtc();
    public IReadOnlyCollection<ApiTokenScope> SelectedScopes => _scopes.CheckedItems.Cast<ApiTokenScope>().ToList();

    private bool ValidateInput()
    {
        string? error = null;
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            error = "Token name is required.";
        }
        else if (_scopes.CheckedItems.Count == 0)
        {
            error = "Select at least one scope.";
        }

        _validation.Text = error ?? string.Empty;
        _okButton.Enabled = string.IsNullOrWhiteSpace(error);
        return string.IsNullOrWhiteSpace(error);
    }

    private sealed record ExpiryOption(string Label, TimeSpan? Duration)
    {
        public DateTime? CreateExpiryUtc() => Duration.HasValue ? DateTime.UtcNow.Add(Duration.Value) : null;
        public override string ToString() => Label;
    }
}

