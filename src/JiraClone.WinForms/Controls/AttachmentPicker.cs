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
    private const int InteractiveHeight = 132;
    private const int ReadOnlyHeight = 152;

    private readonly TextBox _pathTextBox = JiraControlFactory.CreateTextBox();
    private readonly Button _browseButton = JiraControlFactory.CreateSecondaryButton("Browse");
    private readonly Button _uploadButton = JiraControlFactory.CreatePrimaryButton("Upload");
    private readonly Label _readOnlyHint = JiraControlFactory.CreateLabel(string.Empty, true);
    private bool _isUploading;
    private bool _isReadOnly;

    public AttachmentPicker()
    {
        DoubleBuffered = true;
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;
        Height = InteractiveHeight;
        MinimumSize = new Size(0, InteractiveHeight);

        _pathTextBox.ReadOnly = true;
        _pathTextBox.Dock = DockStyle.Fill;
        _pathTextBox.Margin = new Padding(0, 8, 0, 0);

        _browseButton.AutoSize = false;
        _browseButton.Size = new Size(92, 36);
        _browseButton.Margin = new Padding(8, 0, 0, 0);
        _uploadButton.AutoSize = false;
        _uploadButton.Size = new Size(92, 36);
        _uploadButton.Margin = Padding.Empty;

        _readOnlyHint.Dock = DockStyle.Top;
        _readOnlyHint.AutoSize = true;
        _readOnlyHint.Margin = new Padding(0, 4, 0, 0);
        _readOnlyHint.ForeColor = JiraTheme.TextSecondary;
        _readOnlyHint.Visible = false;

        var title = JiraControlFactory.CreateLabel("Drop files here or browse to attach", true);
        title.AutoSize = true;
        title.Margin = Padding.Empty;

        var buttonBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent,
            Padding = new Padding(0),
            Margin = new Padding(0, 8, 0, 0),
            WrapContents = false,
        };
        buttonBar.Controls.Add(_uploadButton);
        buttonBar.Controls.Add(_browseButton);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(JiraTheme.Md),
            BackColor = JiraTheme.BgSurface,
            Margin = Padding.Empty,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Math.Max(_pathTextBox.MinimumSize.Height, 36)));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        layout.Paint += (_, e) =>
        {
            using var pen = new Pen(JiraTheme.Border) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            e.Graphics.DrawRectangle(pen, 0, 0, layout.Width - 1, layout.Height - 1);
        };
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(_readOnlyHint, 0, 1);
        layout.Controls.Add(_pathTextBox, 0, 2);
        layout.Controls.Add(buttonBar, 0, 3);

        _browseButton.Click += (_, _) => BrowseFile();
        _uploadButton.Click += async (_, _) => await UploadAsync();

        Controls.Add(layout);
        UpdateActionState();
    }

    public Func<string, Task>? UploadRequested { get; set; }

    public void SetReadOnlyState(bool isReadOnly, string? message = null)
    {
        _isReadOnly = isReadOnly;
        if (_isReadOnly)
        {
            _pathTextBox.Clear();
        }

        _readOnlyHint.Text = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
        _readOnlyHint.Visible = _isReadOnly && !string.IsNullOrWhiteSpace(_readOnlyHint.Text);
        Height = _isReadOnly ? ReadOnlyHeight : InteractiveHeight;
        MinimumSize = new Size(0, Height);
        UpdateActionState();
    }

    private void BrowseFile()
    {
        if (_isUploading || _isReadOnly)
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
        if (UploadRequested is null || _isUploading || _isReadOnly)
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
        var canInteract = !_isUploading && !_isReadOnly;
        _browseButton.Enabled = canInteract;
        _uploadButton.Enabled = canInteract && hasPath;
    }
}
