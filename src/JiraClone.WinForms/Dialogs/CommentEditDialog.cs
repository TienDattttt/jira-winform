using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Dialogs;

public class CommentEditDialog : Form
{
    private readonly TextBox _bodyTextBox = JiraControlFactory.CreateTextBox();
    private readonly Button _okButton = JiraControlFactory.CreatePrimaryButton("Save");
    private readonly Button _cancelButton = JiraControlFactory.CreateSecondaryButton("Cancel");

    public CommentEditDialog(string title, string initialBody)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(480, 300);
        Size = new Size(520, 340);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;

        _bodyTextBox.Multiline = true;
        _bodyTextBox.ScrollBars = ScrollBars.Vertical;
        _bodyTextBox.Dock = DockStyle.Fill;
        _bodyTextBox.Text = initialBody;
        _bodyTextBox.AcceptsReturn = true;

        _okButton.AutoSize = false;
        _okButton.Size = new Size(100, 38);
        _okButton.DialogResult = DialogResult.OK;
        _okButton.Click += (_, _) => DialogResult = DialogResult.OK;

        _cancelButton.AutoSize = false;
        _cancelButton.Size = new Size(100, 38);
        _cancelButton.DialogResult = DialogResult.Cancel;

        AcceptButton = _okButton;
        CancelButton = _cancelButton;

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(8, 8, 8, 8),
            BackColor = JiraTheme.BgSurface,
        };
        footer.Controls.Add(_okButton);
        footer.Controls.Add(_cancelButton);

        var label = JiraControlFactory.CreateLabel("Comment body:");
        label.Dock = DockStyle.Top;
        label.Height = 28;
        label.Padding = new Padding(8, 8, 8, 0);

        Controls.Add(_bodyTextBox);
        Controls.Add(footer);
        Controls.Add(label);

        Shown += (_, _) =>
        {
            _bodyTextBox.Focus();
            _bodyTextBox.SelectAll();
        };
    }

    public string Body => _bodyTextBox.Text.Trim();
}
