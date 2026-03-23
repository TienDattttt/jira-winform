using JiraClone.Domain.Enums;
using JiraClone.WinForms.Helpers;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public sealed class CreateApiTokenDialog : Form
{
    private readonly TextBox _name = JiraControlFactory.CreateTextBox();
    private readonly ComboBox _expiry = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Font = JiraTheme.FontBody,
        Width = 260,
        IntegralHeight = false,
        Margin = new Padding(0, 0, 0, 8),
    };
    private readonly CheckedListBox _scopes = new()
    {
        CheckOnClick = true,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = JiraTheme.BgSurface,
        ForeColor = JiraTheme.TextPrimary,
        Font = JiraTheme.FontBody,
        Height = 180,
        Width = 420,
        Margin = new Padding(0, 0, 0, 8),
    };
    private readonly Label _validation = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly Button _okButton = JiraControlFactory.CreatePrimaryButton("Create Token");

    public CreateApiTokenDialog()
    {
        Text = "Create API Token";
        LayoutHelper.ConfigureForm(this);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(560, 460);
        Padding = new Padding(0);
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;

        _validation.ForeColor = JiraTheme.Red600;
        _validation.MaximumSize = new Size(420, 0);
        _validation.AutoSize = true;
        _validation.Margin = new Padding(0, 4, 0, 0);

        _expiry.Items.AddRange(
        [
            new ExpiryOption("30 days", TimeSpan.FromDays(30)),
            new ExpiryOption("90 days", TimeSpan.FromDays(90)),
            new ExpiryOption("1 year", TimeSpan.FromDays(365)),
            new ExpiryOption("Never", null),
        ]);
        _expiry.SelectedIndex = 0;
        LayoutHelper.ConfigureComboBox(_expiry);

        foreach (var scope in Enum.GetValues<ApiTokenScope>())
        {
            _scopes.Items.Add(scope, scope is ApiTokenScope.ReadIssues or ApiTokenScope.ReadProjects);
        }

        _name.Width = 420;
        _name.Margin = new Padding(0, 0, 0, 8);
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

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(24, 20, 24, 16),
            BackColor = JiraTheme.BgSurface,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.Controls.Add(CreateSectionLabel("Name"), 0, 0);
        body.Controls.Add(_name, 0, 1);
        body.Controls.Add(CreateSectionLabel("Expiry"), 0, 2);
        body.Controls.Add(_expiry, 0, 3);
        body.Controls.Add(CreateSectionLabel("Scopes"), 0, 4);
        body.Controls.Add(_scopes, 0, 5);
        body.Controls.Add(_validation, 0, 6);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(12, 12, 12, 16),
            BackColor = JiraTheme.BgSurface,
        };
        buttons.Controls.Add(_okButton);
        buttons.Controls.Add(cancelButton);

        Controls.Add(body);
        Controls.Add(buttons);
        AcceptButton = _okButton;
        CancelButton = cancelButton;
        LayoutHelper.EnableResponsiveLayout(this);
        ValidateInput();
    }

    public string TokenName => _name.Text.Trim();
    public DateTime? ExpiresAtUtc => (_expiry.SelectedItem as ExpiryOption)?.CreateExpiryUtc();
    public IReadOnlyCollection<ApiTokenScope> SelectedScopes => _scopes.CheckedItems.Cast<ApiTokenScope>().ToList();

    private static Label CreateSectionLabel(string text)
    {
        var label = JiraControlFactory.CreateLabel(text, true);
        label.Margin = new Padding(0, 0, 0, 6);
        return label;
    }

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

