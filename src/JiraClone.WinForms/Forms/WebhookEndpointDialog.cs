using System.Security.Cryptography;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public sealed class WebhookEndpointDialog : Form
{
    private readonly TextBox _name = JiraControlFactory.CreateTextBox();
    private readonly TextBox _url = JiraControlFactory.CreateTextBox();
    private readonly TextBox _secret = JiraControlFactory.CreateTextBox();
    private readonly Button _generateSecret = JiraControlFactory.CreateSecondaryButton("Generate Secret");
    private readonly CheckBox _active = new() { Text = "Active", AutoSize = true, ForeColor = JiraTheme.TextPrimary, BackColor = JiraTheme.BgSurface, Font = JiraTheme.FontBody };
    private readonly CheckedListBox _events = new()
    {
        CheckOnClick = true,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = JiraTheme.BgSurface,
        ForeColor = JiraTheme.TextPrimary,
        Font = JiraTheme.FontBody,
        Height = 184,
        Width = 360,
    };
    private readonly Label _validation = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly Button _okButton = JiraControlFactory.CreatePrimaryButton("Save");

    public WebhookEndpointDialog(WebhookEndpoint? endpoint = null)
    {
        Text = endpoint is null ? "Add Webhook" : "Edit Webhook";
        AutoScaleMode = AutoScaleMode.Font;
        Width = 520;
        Height = 520;
        MinimumSize = new Size(520, 520);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;

        _validation.ForeColor = JiraTheme.Red600;
        _validation.MaximumSize = new Size(360, 0);
        _validation.AutoSize = true;

        foreach (var eventType in Enum.GetValues<WebhookEventType>())
        {
            _events.Items.Add(eventType, false);
        }

        _name.Width = 360;
        _url.Width = 360;
        _secret.Width = 360;
        _secret.UseSystemPasswordChar = true;

        _generateSecret.Click += (_, _) => GenerateSecret();
        _name.TextChanged += (_, _) => { ValidateInput(); };
        _url.TextChanged += (_, _) => { ValidateInput(); };
        _secret.TextChanged += (_, _) => { ValidateInput(); };
        _active.CheckedChanged += (_, _) => { ValidateInput(); };
        _events.ItemCheck += (_, _) => { BeginInvoke(new Action(() => ValidateInput())); };

        _okButton.Enabled = false;
        _okButton.Text = endpoint is null ? "Add" : "Save";
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

        if (endpoint is null)
        {
            _active.Checked = true;
            GenerateSecret();
        }
        else
        {
            _name.Text = endpoint.Name;
            _url.Text = endpoint.Url;
            _secret.Text = endpoint.Secret;
            _active.Checked = endpoint.IsActive;
            foreach (var subscribedEvent in endpoint.Subscriptions.Select(x => x.EventType))
            {
                var index = _events.Items.IndexOf(subscribedEvent);
                if (index >= 0)
                {
                    _events.SetItemChecked(index, true);
                }
            }
        }

        var secretRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            BackColor = JiraTheme.BgSurface,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        secretRow.Controls.Add(_secret);
        secretRow.Controls.Add(_generateSecret);

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
        layout.Controls.Add(JiraControlFactory.CreateLabel("URL", true));
        layout.Controls.Add(_url);
        layout.Controls.Add(JiraControlFactory.CreateLabel("Secret", true));
        layout.Controls.Add(secretRow);
        layout.Controls.Add(_active);
        layout.Controls.Add(JiraControlFactory.CreateLabel("Subscribed events", true));
        layout.Controls.Add(_events);
        layout.Controls.Add(_validation);

        Controls.Add(layout);
        Controls.Add(buttons);
        AcceptButton = _okButton;
        ValidateInput();
    }

    public string EndpointName => _name.Text.Trim();
    public string EndpointUrl => _url.Text.Trim();
    public string Secret => _secret.Text.Trim();
    public bool IsActive => _active.Checked;
    public IReadOnlyList<WebhookEventType> SubscribedEvents => _events.CheckedItems.Cast<WebhookEventType>().ToList();

    private void GenerateSecret()
    {
        _secret.Text = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    private bool ValidateInput()
    {
        string? error = null;
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            error = "Webhook name is required.";
        }
        else if (!Uri.TryCreate(_url.Text.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = "Webhook URL must be a valid http or https address.";
        }
        else if (string.IsNullOrWhiteSpace(_secret.Text))
        {
            error = "Webhook secret is required.";
        }
        else if (_events.CheckedItems.Count == 0)
        {
            error = "Select at least one event.";
        }

        _validation.Text = error ?? string.Empty;
        _okButton.Enabled = string.IsNullOrWhiteSpace(error);
        return string.IsNullOrWhiteSpace(error);
    }
}


