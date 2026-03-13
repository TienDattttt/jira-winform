using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public class AttachmentPicker : UserControl
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".pdf", ".png", ".jpg", ".jpeg", ".doc", ".docx", ".xls", ".xlsx", ".zip", ".csv"
    };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private readonly TextBox _pathTextBox = JiraControlFactory.CreateTextBox();
    private readonly Button _browseButton = JiraControlFactory.CreateSecondaryButton("Browse");
    private readonly Button _uploadButton = JiraControlFactory.CreatePrimaryButton("Upload");
    private bool _isUploading;

    public AttachmentPicker()
    {
        DoubleBuffered = true;
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;
        Height = 96;

        _pathTextBox.ReadOnly = true;
        _pathTextBox.Dock = DockStyle.Fill;

        _browseButton.AutoSize = false;
        _browseButton.Size = new Size(92, 36);
        _uploadButton.AutoSize = false;
        _uploadButton.Size = new Size(92, 36);

        var title = JiraControlFactory.CreateLabel("Drop files here or browse to attach", true);
        title.Dock = DockStyle.Top;
        title.AutoSize = false;
        title.Height = 22;

        var buttonBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent,
            Padding = new Padding(0),
        };
        buttonBar.Controls.Add(_uploadButton);
        buttonBar.Controls.Add(_browseButton);

        var inner = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(JiraTheme.Md),
            BackColor = JiraTheme.BgSurface,
        };
        inner.Paint += (_, e) =>
        {
            using var pen = new Pen(JiraTheme.Border) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            e.Graphics.DrawRectangle(pen, 0, 0, inner.Width - 1, inner.Height - 1);
        };
        inner.Controls.Add(_pathTextBox);
        inner.Controls.Add(buttonBar);
        inner.Controls.Add(title);

        _browseButton.Click += (_, _) => BrowseFile();
        _uploadButton.Click += async (_, _) => await UploadAsync();

        Controls.Add(inner);
        UpdateActionState();
    }

    public Func<string, Task>? UploadRequested { get; set; }

    private void BrowseFile()
    {
        if (_isUploading)
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Multiselect = false,
            Filter = "Supported files|*.txt;*.pdf;*.png;*.jpg;*.jpeg;*.doc;*.docx;*.xls;*.xlsx;*.zip;*.csv|All files|*.*"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _pathTextBox.Text = dialog.FileName;
            UpdateActionState();
        }
    }

    private async Task UploadAsync()
    {
        if (UploadRequested is null || _isUploading)
        {
            return;
        }

        var path = _pathTextBox.Text;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show(this, "Choose a file first.", "Attachment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var extension = Path.GetExtension(path);
        if (!AllowedExtensions.Contains(extension))
        {
            MessageBox.Show(this, $"Files with extension '{extension}' are not allowed.", "Attachment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (new FileInfo(path).Length > MaxFileSizeBytes)
        {
            MessageBox.Show(this, "File exceeds the 10 MB limit.", "Attachment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _isUploading = true;
            UpdateActionState();
            UseWaitCursor = true;
            await UploadRequested(path);
            _pathTextBox.Clear();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
        finally
        {
            UseWaitCursor = false;
            _isUploading = false;
            UpdateActionState();
        }
    }

    private void UpdateActionState()
    {
        var hasPath = !string.IsNullOrWhiteSpace(_pathTextBox.Text);
        _browseButton.Enabled = !_isUploading;
        _uploadButton.Enabled = !_isUploading && hasPath;
    }
}
