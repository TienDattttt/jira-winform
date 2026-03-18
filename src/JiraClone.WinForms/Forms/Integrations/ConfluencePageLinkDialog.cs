using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms.Integrations;

public sealed class ConfluencePageLinkDialog : Form
{
    private readonly TextBox _title = JiraControlFactory.CreateTextBox();
    private readonly TextBox _url = JiraControlFactory.CreateTextBox();
    private readonly Label _validation = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly Button _ok = JiraControlFactory.CreatePrimaryButton("Add Link");

    public ConfluencePageLinkDialog()
    {
        Text = "Link Confluence Page";
        Width = 500;
        Height = 280;
        MinimumSize = new Size(500, 280);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;

        _validation.ForeColor = JiraTheme.Red600;
        _validation.AutoSize = true;
        _title.TextChanged += (_, _) => ValidateInput();
        _url.TextChanged += (_, _) => ValidateInput();
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

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20), BackColor = JiraTheme.BgSurface, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(JiraControlFactory.CreateLabel("Page title", true), 0, 0);
        layout.Controls.Add(_title, 1, 0);
        layout.Controls.Add(JiraControlFactory.CreateLabel("Page URL", true), 0, 1);
        layout.Controls.Add(_url, 1, 1);
        layout.Controls.Add(_validation, 1, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = JiraTheme.BgSurface };
        buttons.Controls.Add(_ok);
        buttons.Controls.Add(cancel);

        Controls.Add(layout);
        Controls.Add(buttons);
        ValidateInput();
    }

    public string PageTitle => _title.Text.Trim();
    public string PageUrl => _url.Text.Trim();

    private bool ValidateInput()
    {
        string? error = null;
        if (string.IsNullOrWhiteSpace(_url.Text) || !Uri.TryCreate(_url.Text.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = "Page URL must be a valid http or https URL.";
        }

        _validation.Text = error ?? string.Empty;
        _ok.Enabled = string.IsNullOrWhiteSpace(error);
        return string.IsNullOrWhiteSpace(error);
    }
}
