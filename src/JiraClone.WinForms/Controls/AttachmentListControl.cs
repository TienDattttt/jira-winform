using System.Drawing.Text;
using JiraClone.Domain.Entities;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public class AttachmentListControl : UserControl
{
    private readonly FlowLayoutPanel _itemsPanel;
    private readonly Label _emptyState = JiraControlFactory.CreateLabel("No attachments yet.", true);
    private List<Attachment> _attachments = [];

    public AttachmentListControl()
    {
        DoubleBuffered = true;
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;
        Resize += (_, _) => UpdateCardWidths();

        _itemsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = JiraTheme.BgPage,
            Padding = new Padding(0, 0, 8, 0),
            Visible = false,
        };

        _emptyState.Dock = DockStyle.Fill;
        _emptyState.TextAlign = ContentAlignment.MiddleCenter;
        _emptyState.ForeColor = JiraTheme.TextSecondary;

        Controls.Add(_itemsPanel);
        Controls.Add(_emptyState);
    }

    public Func<Attachment, Task>? DownloadRequested { get; set; }
    public Func<Attachment, Task>? DeleteRequested { get; set; }
    public bool AllowDelete { get; set; } = true;

    public void Bind(IReadOnlyList<Attachment> attachments)
    {
        _attachments = attachments.ToList();
        _itemsPanel.SuspendLayout();
        _itemsPanel.Controls.Clear();

        foreach (var attachment in _attachments)
        {
            _itemsPanel.Controls.Add(new AttachmentCard(attachment, DownloadRequested, DeleteRequested, AllowDelete));
        }

        _itemsPanel.ResumeLayout();
        UpdateCardWidths();
        var hasItems = _attachments.Count > 0;
        _itemsPanel.Visible = hasItems;
        _emptyState.Visible = !hasItems;
    }

    private void UpdateCardWidths()
    {
        var width = Math.Max(360, ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8);
        foreach (var card in _itemsPanel.Controls.OfType<AttachmentCard>())
        {
            card.Width = width;
            card.ApplyResponsiveLayout();
        }
    }

    private sealed class AttachmentCard : Panel
    {
        private readonly Label _name;
        private readonly Label _meta;
        private readonly Button _downloadButton;
        private readonly Button _deleteButton;

        public AttachmentCard(Attachment attachment, Func<Attachment, Task>? download, Func<Attachment, Task>? delete, bool allowDelete)
        {
            Width = 520;
            Height = 72;
            Margin = new Padding(0, 0, 0, JiraTheme.Sm);
            Padding = new Padding(JiraTheme.Md);
            BackColor = JiraTheme.BgSurface;

            Paint += (_, e) =>
            {
                using var pen = new Pen(JiraTheme.Border);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };

            var icon = new FileGlyph { Location = new Point(12, 16), Size = new Size(20, 24) };
            _name = JiraControlFactory.CreateLabel(attachment.OriginalFileName);
            _name.Location = new Point(44, 12);
            _name.Font = JiraTheme.FontBody;
            _name.AutoEllipsis = true;

            _meta = JiraControlFactory.CreateLabel($"{attachment.FileSizeBytes:N0} bytes | {attachment.UploadedAtUtc:g}", true);
            _meta.Location = new Point(44, 34);
            _meta.AutoSize = true;

            _downloadButton = JiraControlFactory.CreateSecondaryButton("Download");
            _downloadButton.AutoSize = false;
            _downloadButton.Size = new Size(92, 32);
            _downloadButton.Click += async (_, _) =>
            {
                if (download is not null)
                {
                    await download(attachment);
                }
            };

            _deleteButton = JiraControlFactory.CreateSecondaryButton("Delete");
            _deleteButton.AutoSize = false;
            _deleteButton.Size = new Size(80, 32);
            _deleteButton.Visible = allowDelete && delete is not null;
            _deleteButton.Click += async (_, _) =>
            {
                if (delete is not null)
                {
                    await delete(attachment);
                }
            };

            Controls.Add(icon);
            Controls.Add(_name);
            Controls.Add(_meta);
            Controls.Add(_downloadButton);
            Controls.Add(_deleteButton);

            ApplyResponsiveLayout();
        }

        public void ApplyResponsiveLayout()
        {
            if (_deleteButton.Visible)
            {
                _deleteButton.Location = new Point(Math.Max(Width - 96, 340), 20);
                _downloadButton.Location = new Point(Math.Max(_deleteButton.Left - 100, 240), 20);
            }
            else
            {
                _downloadButton.Location = new Point(Math.Max(Width - 108, 240), 20);
            }

            _name.Width = Math.Max(140, _downloadButton.Left - 56);
        }
    }

    private sealed class FileGlyph : Control
    {
        public FileGlyph()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SetClip(new Rectangle(0, 0, Width, Height));
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using var border = new Pen(JiraTheme.Blue600, 1.5f);
            using var fill = new SolidBrush(Color.FromArgb(236, 244, 255));
            e.Graphics.FillRectangle(fill, 2, 2, Width - 6, Height - 4);
            e.Graphics.DrawRectangle(border, 2, 2, Width - 6, Height - 4);
            e.Graphics.DrawLine(border, Width - 10, 2, Width - 10, 10);
            e.Graphics.DrawLine(border, Width - 10, 10, Width - 2, 10);
        }
    }
}
