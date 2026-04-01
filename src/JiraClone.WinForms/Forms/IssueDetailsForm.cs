using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraLabelEntity = JiraClone.Domain.Entities.Label;
using JiraComponentEntity = JiraClone.Domain.Entities.Component;
using JiraProjectVersionEntity = JiraClone.Domain.Entities.ProjectVersion;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Controls;
using JiraClone.WinForms.Dialogs;
using JiraClone.WinForms.Helpers;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;
using Microsoft.Extensions.Logging;

namespace JiraClone.WinForms.Forms;

public class IssueDetailsForm : Form
{
    private const int DescriptionSectionHeight = 290;
    private const int PreferredLeftPanelMinWidth = 420;
    private const int PreferredRightPanelMinWidth = 260;
    private readonly AppSession _session;
    private readonly int _issueId;
    private readonly int _projectId;
    private readonly ILogger<IssueDetailsForm> _logger;
    private readonly SplitContainer _split = new() { Dock = DockStyle.Fill };
    private readonly Label _breadcrumb = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly Panel _typeHost = new() { Width = 120, Height = 24, BackColor = Color.Transparent };
    private readonly Label _title = JiraControlFactory.CreateLabel(string.Empty);
    private readonly FlowLayoutPanel _metaRow = new() { Dock = DockStyle.Top, Height = 34, WrapContents = false, BackColor = JiraTheme.BgSurface, Margin = new Padding(0), Padding = new Padding(0, 4, 0, 4) };
    private readonly Label _keyBadge = CreateMetaBadge();
    private readonly Label _statusBadge = CreateMetaBadge();
    private readonly Label _priorityBadge = CreateMetaBadge();
    private readonly Label _updatedLabel = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly TextBox _titleEditor = JiraControlFactory.CreateTextBox();
    private readonly MarkdownEditorControl _descriptionEditor = new() { Dock = DockStyle.Fill };
    private readonly MarkdownViewerControl _descriptionViewer = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Button _descriptionEdit = CreateModeButton("Sửa");
    private readonly Button _descriptionPreview = CreateModeButton("Xem trước");
    private readonly AttachmentPicker _attachmentPicker = new() { Dock = DockStyle.Top, Height = 42 };
    private readonly AttachmentListControl _attachments = new() { Dock = DockStyle.Fill };
    private readonly Button _commentsTab = MakeTab("Bình luận", true);
    private readonly Button _historyTab = MakeTab("Lịch sử", false);
    private readonly Button _childIssuesTab = MakeTab("Issue con", false);
    private readonly Panel _tabIndicator = new() { Height = 2, Width = 100, BackColor = JiraTheme.Primary };
    private readonly Panel _commentComposer = new() { Dock = DockStyle.Top, Height = 116, BackColor = JiraTheme.BgSurface, Padding = new Padding(0, 8, 0, 8) };
    private readonly TextBox _commentInput = JiraControlFactory.CreateTextBox();
    private readonly Button _saveComment = JiraControlFactory.CreatePrimaryButton("Lưu");
    private readonly CommentListControl _comments = new() { Dock = DockStyle.Fill };
    private readonly ActivityTimelineControl _history = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Panel _childIssuesView = new() { Dock = DockStyle.Fill, Visible = false, BackColor = JiraTheme.BgSurface };
    private readonly FlowLayoutPanel _childIssuesPanel = new() { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Visible = true, BackColor = JiraTheme.BgSurface, Padding = new Padding(0, 4, 0, 4) };
    private readonly Button _addExistingIssueButton = JiraControlFactory.CreateSecondaryButton("Thêm issue có sẵn");
    private readonly Button _createChildIssueButton = JiraControlFactory.CreatePrimaryButton("Tạo issue con");
    private readonly ComboBox _status = new() { DrawMode = DrawMode.OwnerDrawFixed, DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly ComboBox _priority = new() { DrawMode = DrawMode.OwnerDrawFixed, DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly SprintSelectorControl _sprint = new() { Width = 180 };
    private readonly NumericUpDown _storyPoints = new() { Width = 180, Minimum = 0, Maximum = 100, BorderStyle = BorderStyle.FixedSingle };
    private readonly TimeTrackingBar _time = new() { Dock = DockStyle.Top, Height = 52 };
    private readonly Button _logTime = JiraControlFactory.CreateSecondaryButton("Ghi thời gian");
    private readonly AvatarValueControl _assignee = new() { Width = 220, Cursor = Cursors.Hand };
    private readonly AvatarValueControl _reporter = new() { Width = 220 };
    private readonly FlowLayoutPanel _labelsField = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, MaximumSize = new Size(220, 0), Margin = new Padding(0), BackColor = Color.Transparent };
    private readonly Button _editLabels = JiraControlFactory.CreateSecondaryButton("Sửa nhãn");
    private readonly ComboBox _component = CreateValueCombo();
    private readonly ComboBox _fixVersion = CreateValueCombo();
    private readonly Panel _dueDateField = new() { Width = 220, Height = 36, BackColor = Color.Transparent };
    private readonly LinkLabel _dueDateDisplay = new() { Dock = DockStyle.Fill, AutoSize = false, Height = 32, Font = JiraTheme.FontSmall, LinkBehavior = LinkBehavior.HoverUnderline, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, Cursor = Cursors.Hand };
    private readonly DateTimePicker _dueDatePicker = new() { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Custom, CustomFormat = "dd MMM yyyy", ShowCheckBox = true, Visible = false, CalendarForeColor = JiraTheme.TextPrimary, CalendarMonthBackground = JiraTheme.BgSurface, Font = JiraTheme.FontBody };
    private readonly Panel _watchersField = new() { Width = 220, Height = 58, BackColor = Color.Transparent };
    private readonly Label _watcherCountLabel = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly FlowLayoutPanel _watcherAvatars = new() { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = false, Margin = new Padding(0), Padding = new Padding(0), BackColor = Color.Transparent };
    private readonly Button _watchButton = JiraControlFactory.CreateSecondaryButton("Theo dõi");
    private readonly IssueIntegrationsControl _integrations;
    private readonly Button _delete = JiraControlFactory.CreateSecondaryButton("Xóa");
    private readonly ToolStripMenuItem _deleteIssueMenuItem = new("Xóa issue");
    private readonly LinkLabel _parentLink = new() { AutoSize = true, Font = JiraTheme.FontBody, LinkColor = JiraTheme.PrimaryActive, Visible = false };
    private readonly Panel _parentSection = new() { Dock = DockStyle.Top, Height = 40, Visible = false, BackColor = JiraTheme.BgSurface };
    private IssueDetailsDto? _details;
    private IReadOnlyList<User> _users = Array.Empty<User>();
    private IReadOnlyList<JiraLabelEntity> _availableLabels = Array.Empty<JiraLabelEntity>();
    private IReadOnlyList<JiraComponentEntity> _availableComponents = Array.Empty<JiraComponentEntity>();
    private IReadOnlyList<JiraProjectVersionEntity> _availableVersions = Array.Empty<JiraProjectVersionEntity>();
    private IReadOnlyList<User> _watchers = Array.Empty<User>();
    private IReadOnlyList<WorkflowStatusOptionDto> _statusOptions = Array.Empty<WorkflowStatusOptionDto>();
    private HashSet<int> _selectedAssigneeIds = [];
    private HashSet<int> _selectedLabelIds = [];
    private bool _loading;
    private string _descriptionMarkdown = string.Empty;
    private bool _descriptionPreviewMode;
    private bool _descriptionModeInitialized;
    private bool _dueDateEditing;
    private bool _isWatching;
    private readonly bool _openChildIssuesByDefault;
    private readonly CancellationTokenSource _disposeCts = new();
    private CancellationTokenSource? _loadCts;

    public IssueDetailsForm(AppSession session, int issueId, int projectId, bool openChildIssues = false)
    {
        _session = session;
        _issueId = issueId;
        _projectId = projectId;
        _openChildIssuesByDefault = openChildIssues;
        _logger = session.CreateLogger<IssueDetailsForm>();
        _integrations = new IssueIntegrationsControl(_session);
        Text = "Chi tiết issue";
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(900, 600);
        Size = new Size(1180, 760);
        BackColor = JiraTheme.BgPage;
        DoubleBuffered = true;
        _split.FixedPanel = FixedPanel.Panel2;

        _split.Panel1.BackColor = JiraTheme.BgSurface;
        _split.Panel2.BackColor = JiraTheme.BgSurface;
        Controls.Add(_split);

        _title.Font = JiraTheme.FontH1;
        _title.AutoSize = false;
        _title.Height = 42;
        _title.Click += OnTitleClicked;
        _titleEditor.Font = JiraTheme.FontH1;
        _titleEditor.Visible = false;
        _titleEditor.KeyDown += OnTitleEditorKeyDown;
        _titleEditor.Leave += OnTitleEditorLeave;

        _descriptionEdit.Click += OnDescriptionEditClick;
        _descriptionPreview.Click += OnDescriptionPreviewClick;
        _descriptionEditor.EditorLeave += OnDescriptionEditorLeave;

        _commentInput.Multiline = true;
        _commentInput.Height = 72;
        _saveComment.AutoSize = false;
        _saveComment.Size = new Size(90, 36);
        _saveComment.Click += OnSaveCommentClick;
        _childIssuesTab.Width = 116;
        _childIssuesTab.Click += OnChildIssuesTabClick;
        _addExistingIssueButton.AutoSize = false;
        _addExistingIssueButton.Size = new Size(148, 34);
        _addExistingIssueButton.Click += OnAddExistingIssueButtonClick;
        _createChildIssueButton.AutoSize = false;
        _createChildIssueButton.Size = new Size(136, 34);
        _createChildIssueButton.Click += OnCreateChildIssueButtonClick;
        _parentLink.LinkClicked += OnParentLinkClicked;

        _status.DisplayMember = nameof(WorkflowStatusOptionDto.Name);
        _status.ValueMember = nameof(WorkflowStatusOptionDto.Id);
        _status.DrawItem += DrawStatus;
        _status.SelectedIndexChanged += OnStatusSelectedIndexChanged;
        _priority.DataSource = Enum.GetValues<IssuePriority>();
        _priority.DrawItem += DrawPriority;
        _priority.SelectedIndexChanged += OnPrioritySelectedIndexChanged;
        _sprint.SelectedIndexChanged += OnSprintSelectedIndexChanged;
        _storyPoints.ValueChanged += OnStoryPointsValueChanged;
        _assignee.Click += OnAssigneeClick;
        _editLabels.AutoSize = false;
        _editLabels.Size = new Size(96, 28);
        _editLabels.Margin = new Padding(0, 0, 0, 0);
        _editLabels.Click += OnEditLabelsClick;
        _component.DisplayMember = nameof(LookupOption.Text);
        _component.ValueMember = nameof(LookupOption.Value);
        _component.SelectedIndexChanged += OnComponentSelectedIndexChanged;
        _fixVersion.DisplayMember = nameof(LookupOption.Text);
        _fixVersion.ValueMember = nameof(LookupOption.Value);
        _fixVersion.SelectedIndexChanged += OnFixVersionSelectedIndexChanged;
        ConfigureDueDateField();
        _dueDateDisplay.LinkClicked += OnDueDateDisplayLinkClicked;
        _dueDateDisplay.Click += OnDueDateDisplayClick;
        _dueDatePicker.CloseUp += OnDueDatePickerCloseUp;
        _dueDatePicker.Leave += OnDueDatePickerLeave;
        _dueDatePicker.KeyDown += OnDueDatePickerKeyDown;
        _logTime.AutoSize = false;
        ConfigureWatchField();
        _watchButton.Click += OnWatchButtonClick;
        _integrations.DataChanged += OnIntegrationsDataChanged;
        _logTime.Size = new Size(100, 36);
        _logTime.Click += OnLogTimeClick;
        _delete.AutoSize = false;
        _delete.Size = new Size(88, 34);
        _delete.Click += OnDeleteButtonClick;

        _comments.EditRequested = EditCommentAsync;
        _comments.DeleteRequested = DeleteCommentAsync;
        _attachments.DownloadRequested = DownloadAttachmentAsync;
        _attachments.DeleteRequested = DeleteAttachmentAsync;
        _attachmentPicker.UploadRequested = UploadAttachmentAsync;
        ContextMenuStrip = new ContextMenuStrip();
        _deleteIssueMenuItem.Click += OnDeleteIssueMenuItemClick;
        ContextMenuStrip.Items.Add(_deleteIssueMenuItem);

        _split.Panel1.Controls.Add(BuildLeft());
        _split.Panel2.Controls.Add(BuildRight());
        Load += OnIssueDetailsLoad;
        Shown += OnIssueDetailsShown;
        Resize += OnIssueDetailsResize;
        _split.SizeChanged += OnSplitSizeChanged;
    }

    private Task ReloadDetailsAsync(bool refreshReferenceData = true, CancellationToken cancellationToken = default) =>
        LoadDetailsAsync(refreshReferenceData, RestartLoadCancellation(cancellationToken));

    private CancellationToken RestartLoadCancellation(CancellationToken cancellationToken = default)
    {
        CancelPendingLoad();
        _loadCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, cancellationToken);
        return _loadCts.Token;
    }

    private void CancelPendingLoad()
    {
        if (_loadCts is null)
        {
            return;
        }

        try
        {
            _loadCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _loadCts.Dispose();
        _loadCts = null;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        CancelPendingLoad();
        _disposeCts.Cancel();
        base.OnFormClosing(e);
    }

    private void OnTitleClicked(object? sender, EventArgs e)
    {
        BeginTitleEdit();
    }

    private async void OnTitleEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            await CommitTitleAsync();
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            CancelTitleEdit();
        }
    }

    private async void OnTitleEditorLeave(object? sender, EventArgs e)
    {
        await CommitTitleAsync();
    }

    private void OnDescriptionEditClick(object? sender, EventArgs e)
    {
        SetDescriptionMode(false, true);
    }

    private async void OnDescriptionPreviewClick(object? sender, EventArgs e)
    {
        await ShowDescriptionPreviewAsync();
    }

    private async void OnDescriptionEditorLeave(object? sender, EventArgs e)
    {
        await SaveDescriptionAsync();
    }

    private async void OnSaveCommentClick(object? sender, EventArgs e)
    {
        await AddCommentAsync();
    }

    private void OnCommentsTabClick(object? sender, EventArgs e)
    {
        ActivateTab(DetailsTab.Comments);
    }

    private void OnHistoryTabClick(object? sender, EventArgs e)
    {
        ActivateTab(DetailsTab.History);
    }

    private void OnChildIssuesTabClick(object? sender, EventArgs e)
    {
        ActivateTab(DetailsTab.ChildIssues);
    }

    private async void OnAddExistingIssueButtonClick(object? sender, EventArgs e)
    {
        await AddExistingChildIssueAsync();
    }

    private async void OnCreateChildIssueButtonClick(object? sender, EventArgs e)
    {
        await CreateChildIssueAsync();
    }

    private void OnParentLinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (e.Link?.LinkData is not int parentIssueId)
        {
            return;
        }

        using var dialog = new IssueDetailsForm(_session, parentIssueId, _projectId);
        dialog.ShowDialog(this);
    }

    private async void OnStatusSelectedIndexChanged(object? sender, EventArgs e)
    {
        await SaveIssueAsync();
    }

    private async void OnPrioritySelectedIndexChanged(object? sender, EventArgs e)
    {
        await SaveIssueAsync();
    }

    private async void OnSprintSelectedIndexChanged(object? sender, EventArgs e)
    {
        await SaveIssueAsync();
    }

    private async void OnStoryPointsValueChanged(object? sender, EventArgs e)
    {
        if (_storyPoints.Focused)
        {
            await SaveIssueAsync();
        }
    }

    private async void OnAssigneeClick(object? sender, EventArgs e)
    {
        await ShowAssigneePickerAsync();
    }

    private async void OnEditLabelsClick(object? sender, EventArgs e)
    {
        await ShowLabelPickerAsync();
    }

    private async void OnComponentSelectedIndexChanged(object? sender, EventArgs e)
    {
        await SaveComponentAsync();
    }

    private async void OnFixVersionSelectedIndexChanged(object? sender, EventArgs e)
    {
        await SaveFixVersionAsync();
    }

    private void OnDueDateDisplayLinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        BeginDueDateEdit();
    }

    private void OnDueDateDisplayClick(object? sender, EventArgs e)
    {
        BeginDueDateEdit();
    }

    private async void OnDueDatePickerCloseUp(object? sender, EventArgs e)
    {
        await CommitDueDateAsync();
    }

    private async void OnDueDatePickerLeave(object? sender, EventArgs e)
    {
        await CommitDueDateAsync();
    }

    private async void OnDueDatePickerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            await CommitDueDateAsync();
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            CancelDueDateEdit();
        }
    }

    private async void OnWatchButtonClick(object? sender, EventArgs e)
    {
        await ToggleWatchAsync();
    }

    private async void OnIntegrationsDataChanged(object? sender, EventArgs e)
    {
        await ReloadDetailsAsync(false, _disposeCts.Token);
    }

    private async void OnLogTimeClick(object? sender, EventArgs e)
    {
        await LogTimeAsync();
    }

    private async void OnDeleteButtonClick(object? sender, EventArgs e)
    {
        await DeleteIssueAsync();
    }

    private async void OnDeleteIssueMenuItemClick(object? sender, EventArgs e)
    {
        await DeleteIssueAsync();
    }

    private void OnIssueDetailsLoad(object? sender, EventArgs e)
    {
        QueueSafeUpdateSplitLayout();
    }

    private async void OnIssueDetailsShown(object? sender, EventArgs e)
    {
        QueueSafeUpdateSplitLayout();
        await ReloadDetailsAsync(cancellationToken: _disposeCts.Token);
        QueueSafeUpdateSplitLayout();
    }

    private void OnIssueDetailsResize(object? sender, EventArgs e)
    {
        QueueSafeUpdateSplitLayout();
    }

    private void OnSplitSizeChanged(object? sender, EventArgs e)
    {
        QueueSafeUpdateSplitLayout();
    }

    private void QueueSafeUpdateSplitLayout()
    {
        if (IsDisposed)
        {
            return;
        }

        if (!IsHandleCreated)
        {
            return;
        }

        BeginInvoke((MethodInvoker)SafeUpdateSplitLayout);
    }

    private void SafeUpdateSplitLayout()
    {
        if (!IsHandleCreated || _split.Width <= 0)
        {
            return;
        }

        try
        {
            var totalWidth = _split.Width - _split.SplitterWidth;
            if (totalWidth <= 0)
            {
                return;
            }

            var rightMin = Math.Min(PreferredRightPanelMinWidth, Math.Max(220, totalWidth / 3));
            var leftMin = Math.Min(PreferredLeftPanelMinWidth, Math.Max(280, totalWidth - rightMin - 24));
            if (leftMin + rightMin >= totalWidth)
            {
                leftMin = Math.Max(220, totalWidth - rightMin - 16);
            }

            if (leftMin + rightMin >= totalWidth)
            {
                rightMin = Math.Max(180, totalWidth - leftMin - 16);
            }

            if (leftMin <= 0 || rightMin <= 0 || leftMin + rightMin >= totalWidth)
            {
                return;
            }

            var preferred = (int)Math.Round(totalWidth * 0.64);
            SplitContainerHelper.ConfigureSafeLayout(_split, preferred, leftMin, rightMin);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogDebug(ex, "Split layout range error while sizing IssueDetailsForm.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(ex, "Split layout operation error while sizing IssueDetailsForm.");
        }
    }

    private Control BuildLeft()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface, Padding = new Padding(24), AutoScroll = true };
        var crumbs = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, WrapContents = false, BackColor = JiraTheme.BgSurface };
        crumbs.Controls.Add(_breadcrumb);
        crumbs.Controls.Add(_typeHost);
        var titleHost = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = JiraTheme.BgSurface };
        _title.Dock = DockStyle.Fill;
        _titleEditor.Dock = DockStyle.Fill;
        titleHost.Controls.Add(_titleEditor);
        titleHost.Controls.Add(_title);

        _updatedLabel.Font = JiraTheme.FontCaption;
        _updatedLabel.ForeColor = JiraTheme.TextSecondary;
        _updatedLabel.Margin = new Padding(8, 6, 0, 0);
        _metaRow.Controls.Add(_keyBadge);
        _metaRow.Controls.Add(_statusBadge);
        _metaRow.Controls.Add(_priorityBadge);
        _metaRow.Controls.Add(_updatedLabel);

        panel.Controls.Add(BuildActivity());
        panel.Controls.Add(JiraControlFactory.CreateSeparator());
        panel.Controls.Add(BuildAttachments());
        panel.Controls.Add(JiraControlFactory.CreateSeparator());
        panel.Controls.Add(BuildDescriptionSection());
        panel.Controls.Add(_metaRow);
        panel.Controls.Add(titleHost);
        panel.Controls.Add(crumbs);
        return panel;
    }

    private Control BuildDescriptionSection()
    {
        var host = new Panel
        {
            Dock = DockStyle.Top,
            Height = DescriptionSectionHeight,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(0, 8, 0, 8)
        };

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = JiraTheme.BgSurface
        };

        var label = TopLabel("Mô tả");
        label.Dock = DockStyle.Left;
        label.Margin = new Padding(0, 8, 0, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 188,
            Height = 36,
            WrapContents = false,
            BackColor = JiraTheme.BgSurface,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };

        _descriptionEdit.AutoSize = false;
        _descriptionEdit.Size = new Size(76, 32);
        _descriptionEdit.Margin = new Padding(0, 0, 8, 0);
        _descriptionPreview.AutoSize = false;
        _descriptionPreview.Size = new Size(96, 32);
        _descriptionPreview.Margin = new Padding(0);
        actions.Controls.Add(_descriptionEdit);
        actions.Controls.Add(_descriptionPreview);

        header.Controls.Add(actions);
        header.Controls.Add(label);

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(0, 8, 0, 0)
        };
        content.Controls.Add(_descriptionViewer);
        content.Controls.Add(_descriptionEditor);

        host.Controls.Add(content);
        host.Controls.Add(header);
        return host;
    }

    private Control BuildAttachments()
    {
        var host = new Panel { Dock = DockStyle.Top, Height = 230, BackColor = JiraTheme.BgSurface, Padding = new Padding(0, 8, 0, 8) };
        host.Controls.Add(_attachments);
        host.Controls.Add(_attachmentPicker);
        host.Controls.Add(TopLabel("Tệp đính kèm"));
        return host;
    }

    private Control BuildActivity()
    {
        var host = new Panel { Dock = DockStyle.Top, Height = 384, BackColor = JiraTheme.BgSurface, Padding = new Padding(0, 8, 0, 0) };
        var tabs = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = JiraTheme.BgSurface };
        _commentsTab.Location = new Point(0, 0);
        _historyTab.Location = new Point(104, 0);
        _childIssuesTab.Location = new Point(198, 0);
        _commentsTab.Click += OnCommentsTabClick;
        _historyTab.Click += OnHistoryTabClick;
        _tabIndicator.Location = new Point(0, 32);
        tabs.Controls.Add(_tabIndicator);
        tabs.Controls.Add(_childIssuesTab);
        tabs.Controls.Add(_historyTab);
        tabs.Controls.Add(_commentsTab);

        _commentComposer.Controls.Clear();
        _commentInput.Dock = DockStyle.Fill;
        _saveComment.Dock = DockStyle.Bottom;
        _saveComment.Height = 36;
        _commentComposer.Controls.Add(_commentInput);
        _commentComposer.Controls.Add(_saveComment);

        _childIssuesView.Controls.Clear();
        var childActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            WrapContents = false,
            BackColor = JiraTheme.BgSurface,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 0, 0, 8),
            Visible = false
        };
        childActions.Controls.Add(_createChildIssueButton);
        childActions.Controls.Add(_addExistingIssueButton);
        _createChildIssueButton.Margin = new Padding(0, 0, 8, 0);
        _addExistingIssueButton.Margin = new Padding(0);

        var childScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = JiraTheme.BgSurface };
        childScroll.Controls.Add(_childIssuesPanel);
        _childIssuesView.Controls.Add(childScroll);
        _childIssuesView.Controls.Add(childActions);
        _childIssuesView.Tag = childActions;

        var content = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
        content.Controls.Add(_comments);
        content.Controls.Add(_history);
        content.Controls.Add(_childIssuesView);

        host.Controls.Add(content);
        host.Controls.Add(_commentComposer);
        host.Controls.Add(tabs);
        host.Controls.Add(TopLabel("Hoạt động"));
        return host;
    }

    private Control BuildRight()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface, Padding = new Padding(20, 24, 20, 24), AutoScroll = true };
        var detailsLabel = JiraControlFactory.CreateLabel("Chi tiết", true);
        detailsLabel.Dock = DockStyle.Top;
        detailsLabel.Font = JiraTheme.FontCaption;
        detailsLabel.Height = 24;

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = JiraTheme.BgSurface };
        _delete.Dock = DockStyle.Right;
        _watchButton.Dock = DockStyle.Left;
        toolbar.Controls.Add(_delete);
        toolbar.Controls.Add(_watchButton);

        var statusHost = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = JiraTheme.BgSurface, Padding = new Padding(0, 4, 0, 0) };
        _status.Dock = DockStyle.Left;
        statusHost.Controls.Add(_status);

        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, BackColor = JiraTheme.BgSurface, Padding = new Padding(0, 8, 0, 8) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddField(fields, 0, "Người được giao", _assignee);
        AddField(fields, 1, "Người theo dõi", _watchersField);
        AddField(fields, 2, "Người báo cáo", _reporter);
        AddField(fields, 3, "Nhãn", _labelsField);
        AddField(fields, 4, "Thành phần", _component);
        AddField(fields, 5, "Phiên bản sửa lỗi", _fixVersion);
        AddField(fields, 6, "Hạn chót", _dueDateField);
        AddField(fields, 7, "Độ ưu tiên", _priority);
        AddField(fields, 8, "Sprint", _sprint);
        AddField(fields, 9, "Điểm câu chuyện", _storyPoints);

        var detailsBlock = new Panel { Dock = DockStyle.Top, Height = 520, BackColor = JiraTheme.BgSurface };
        detailsBlock.Controls.Add(fields);
        detailsBlock.Controls.Add(statusHost);
        detailsBlock.Controls.Add(TopLabel("Trạng thái"));

        var integrationsBlock = new Panel { Dock = DockStyle.Top, Height = 330, BackColor = JiraTheme.BgSurface, Padding = new Padding(0, 8, 0, 0) };
        _integrations.Dock = DockStyle.Fill;
        integrationsBlock.Controls.Add(_integrations);

        var timeBlock = new Panel { Dock = DockStyle.Top, Height = 114, BackColor = JiraTheme.BgSurface, Padding = new Padding(0, 8, 0, 0) };
        _logTime.Dock = DockStyle.Top;
        _time.Dock = DockStyle.Top;
        timeBlock.Controls.Add(_logTime);
        timeBlock.Controls.Add(_time);

        _parentLink.Dock = DockStyle.Fill;
        _parentSection.Controls.Add(_parentLink);
        var parentLabel = TopLabel("Issue cha");

        panel.Controls.Add(integrationsBlock);
        panel.Controls.Add(TopLabel("Tích hợp"));
        panel.Controls.Add(JiraControlFactory.CreateSeparator());
        panel.Controls.Add(timeBlock);
        panel.Controls.Add(TopLabel("Theo dõi thời gian"));
        panel.Controls.Add(JiraControlFactory.CreateSeparator());
        panel.Controls.Add(_parentSection);
        panel.Controls.Add(parentLabel);
        panel.Controls.Add(JiraControlFactory.CreateSeparator());
        panel.Controls.Add(detailsBlock);
        panel.Controls.Add(detailsLabel);
        panel.Controls.Add(toolbar);
        return panel;
    }

    private async Task LoadDetailsAsync(bool refreshReferenceData = true, CancellationToken cancellationToken = default)
    {
        if (IsDisposed || !Visible)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _loading = true;
            if (refreshReferenceData || _users.Count == 0)
            {
                _users = await _session.Users.GetProjectUsersAsync(_projectId, cancellationToken);
                _availableLabels = await _session.Labels.GetByProjectAsync(_projectId, cancellationToken);
                _availableComponents = await _session.Components.GetByProjectAsync(_projectId, cancellationToken);
                _availableVersions = await _session.Versions.GetByProjectAsync(_projectId, cancellationToken);
                _sprint.Bind(await _session.Sprints.GetByProjectAsync(_projectId, cancellationToken));
                BindMetadataOptions();
            }

            _details = await _session.Issues.GetDetailsAsync(_issueId, cancellationToken);
            if (_details is null)
            {
                ErrorDialogService.Show("Không tìm thấy issue.");
                Close();
                return;
            }

            _watchers = await _session.Watchers.GetWatchersAsync(_issueId, _disposeCts.Token);
            var currentUserId = _session.CurrentUserContext.CurrentUser?.Id ?? 0;
            _isWatching = currentUserId > 0 && await _session.Watchers.IsWatchingAsync(_issueId, currentUserId, cancellationToken);
            await BindStatusOptionsAsync(_details.Issue);
            cancellationToken.ThrowIfCancellationRequested();
            BindIssue(_details);
            _comments.Bind(_details.Comments);
            _attachments.Bind(_details.Attachments);
            _history.Bind(_details.ActivityLogs);
            await _integrations.LoadAsync(_issueId, _projectId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ActivateTab(ResolveInitialTab(_details));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _disposeCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ErrorDialogService.Show(ex);
        }
        finally
        {
            _loading = false;
        }
    }

    private void BindIssue(IssueDetailsDto details)
    {
        var issue = details.Issue;
        _breadcrumb.Text = issue.IssueKey;
        _title.Text = issue.Title;
        _titleEditor.Text = issue.Title;
        ApplyMetaBadge(_keyBadge, issue.IssueKey, JiraTheme.Blue100, JiraTheme.PrimaryActive);
        var statusCategory = issue.WorkflowStatus?.Category ?? StatusCategory.ToDo;
        var statusColor = ParseStatusColor(issue.WorkflowStatus?.Color, statusCategory);
        ApplyMetaBadge(_statusBadge, IssueDisplayText.TranslateStatus(issue.WorkflowStatus?.Name ?? "Không rõ"), statusColor, GetStatusTextColor(statusCategory));
        ApplyMetaBadge(_priorityBadge, TranslateIssuePriority(issue.Priority), issue.Priority switch
        {
            IssuePriority.Low => JiraTheme.Success,
            IssuePriority.Medium => JiraTheme.Warning,
            IssuePriority.High or IssuePriority.Highest => JiraTheme.Danger,
            _ => JiraTheme.Border
        }, issue.Priority == IssuePriority.Medium ? JiraTheme.TextPrimary : Color.White);
        _updatedLabel.Text = $"Cập nhật {issue.UpdatedAtUtc:g}";
        _typeHost.Controls.Clear();
        var badge = JiraBadge.ForType(issue.Type);
        badge.Location = new Point(8, 0);
        _typeHost.Controls.Add(badge);

        if (!_descriptionModeInitialized)
        {
            _descriptionPreviewMode = !string.IsNullOrWhiteSpace(issue.DescriptionText);
            _descriptionModeInitialized = true;
        }

        BindDescription(issue.DescriptionText);

        _status.SelectedValue = issue.WorkflowStatusId;
        _priority.SelectedItem = issue.Priority;
        if (issue.SprintId.HasValue) _sprint.SelectedValue = issue.SprintId.Value; else _sprint.SelectedIndex = -1;
        _storyPoints.Value = Math.Max(_storyPoints.Minimum, Math.Min(_storyPoints.Maximum, issue.StoryPoints ?? 0));

        _selectedLabelIds = issue.IssueLabels.Select(x => x.LabelId).ToHashSet();
        RenderLabelChips();
        SelectLookupValue(_component, issue.IssueComponents.Select(x => (int?)x.ComponentId).FirstOrDefault());
        SelectLookupValue(_fixVersion, issue.FixVersionId);
        BindDueDate(issue);
        RenderWatchers();

        _selectedAssigneeIds = issue.Assignees.Select(x => x.UserId).ToHashSet();
        ApplyAssigneeSummary(issue.Assignees.Select(x => x.User.DisplayName).ToList());
        _reporter.SetPerson(issue.Reporter.DisplayName, Initials(issue.Reporter.DisplayName), false);
        _time.EstimatedHours = issue.EstimateHours ?? 0;
        _time.LoggedHours = issue.TimeSpentHours ?? 0;
        _time.RemainingHours = issue.TimeRemainingHours ?? Math.Max(0, _time.EstimatedHours - _time.LoggedHours);
        _time.Invalidate();

        if (issue.ParentIssue is not null)
        {
            _parentLink.Text = $"{issue.ParentIssue.IssueKey} - {issue.ParentIssue.Title}";
            _parentLink.Links.Clear();
            _parentLink.Links.Add(0, _parentLink.Text.Length, issue.ParentIssue.Id);
            _parentSection.Visible = true;
        }
        else
        {
            _parentSection.Visible = false;
        }

        RenderChildIssues(details);
    }


    private void RenderChildIssues(IssueDetailsDto details)
    {
        _childIssuesPanel.SuspendLayout();
        try
        {
            foreach (Control control in _childIssuesPanel.Controls)
            {
                control.Dispose();
            }

            _childIssuesPanel.Controls.Clear();
            if (details.SubIssues.Count == 0)
            {
                var empty = JiraControlFactory.CreateLabel(
                    details.Issue.Type == IssueType.Epic ? "Chưa có issue con nào liên kết với epic này." : "Không có issue con.",
                    true);
                empty.AutoSize = true;
                empty.ForeColor = JiraTheme.TextSecondary;
                empty.Margin = new Padding(0, 4, 0, 0);
                _childIssuesPanel.Controls.Add(empty);
            }
            else
            {
                foreach (var child in details.SubIssues.OrderBy(issue => issue.WorkflowStatus.DisplayOrder).ThenBy(issue => issue.Title))
                {
                    _childIssuesPanel.Controls.Add(CreateChildIssueRow(child));
                }
            }
        }
        finally
        {
            _childIssuesPanel.ResumeLayout();
        }

        var showChildTab = details.Issue.Type == IssueType.Epic || details.SubIssues.Count > 0;
        _childIssuesTab.Visible = showChildTab;
        if (_childIssuesView.Tag is Control actions)
        {
            actions.Visible = details.Issue.Type == IssueType.Epic;
        }
    }

    private Control CreateChildIssueRow(Issue child)
    {
        var row = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            Width = 520,
            BackColor = JiraTheme.BgSurface,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(0, 4, 0, 4),
            Cursor = Cursors.Hand
        };
        void OnRowPaint(object? sender, PaintEventArgs e)
        {
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
        }

        row.Paint += OnRowPaint;

        var badge = JiraBadge.ForType(child.Type);
        badge.Location = new Point(0, 12);

        var titleLink = new LinkLabel
        {
            AutoSize = false,
            Location = new Point(78, 4),
            Size = new Size(360, 22),
            Text = $"{child.IssueKey} - {child.Title}",
            Font = JiraTheme.FontSmall,
            LinkColor = JiraTheme.PrimaryActive,
            ActiveLinkColor = JiraTheme.PrimaryHover,
            VisitedLinkColor = JiraTheme.PrimaryActive,
            LinkBehavior = LinkBehavior.HoverUnderline,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        };

        var meta = JiraControlFactory.CreateLabel(FormatChildIssueMeta(child), true);
        meta.Location = new Point(78, 26);
        meta.Size = new Size(360, 18);
        meta.ForeColor = JiraTheme.TextSecondary;
        meta.Font = JiraTheme.FontCaption;

        var status = JiraControlFactory.CreateLabel(IssueDisplayText.TranslateStatus(child.WorkflowStatus.Name), true);
        status.AutoSize = true;
        status.BackColor = ParseStatusColor(child.WorkflowStatus.Color, child.WorkflowStatus.Category);
        status.ForeColor = GetStatusTextColor(child.WorkflowStatus.Category);
        status.Padding = new Padding(10, 4, 10, 4);
        status.Location = new Point(452, 11);

        void OpenChild(object? sender, EventArgs e)
        {
            using var dialog = new IssueDetailsForm(_session, child.Id, _projectId);
            dialog.ShowDialog(this);
        }

        void OnTitleLinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenChild(sender, EventArgs.Empty);
        }

        row.Click += OpenChild;
        titleLink.LinkClicked += OnTitleLinkClicked;
        foreach (Control nested in new Control[] { badge, meta, status })
        {
            nested.Click += OpenChild;
        }

        row.Controls.Add(status);
        row.Controls.Add(meta);
        row.Controls.Add(titleLink);
        row.Controls.Add(badge);
        return row;
    }

    private static string FormatChildIssueMeta(Issue child)
    {
        var storyPoints = child.StoryPoints.HasValue ? $" | {child.StoryPoints.Value} điểm" : string.Empty;
        return $"{IssueDisplayText.TranslateType(child.Type)} | {IssueDisplayText.TranslateStatus(child.WorkflowStatus.Name)}{storyPoints}";
    }

    private DetailsTab ResolveInitialTab(IssueDetailsDto details)
    {
        var showChildTab = details.Issue.Type == IssueType.Epic || details.SubIssues.Count > 0;
        if (showChildTab && (_openChildIssuesByDefault || details.Issue.Type == IssueType.Epic))
        {
            return DetailsTab.ChildIssues;
        }

        return DetailsTab.Comments;
    }

    private async Task AddExistingChildIssueAsync()
    {
        if (_details is null || _details.Issue.Type != IssueType.Epic)
        {
            return;
        }

        try
        {
            var candidates = (await _session.Board.GetBoardAsync(_projectId))
                .SelectMany(column => column.Issues)
                .GroupBy(issue => issue.Id)
                .Select(group => group.First())
                .Where(issue => issue.Id != _details.Issue.Id && issue.Type is IssueType.Story or IssueType.Task && issue.EpicId != _details.Issue.Id)
                .OrderBy(issue => issue.IssueKey)
                .ToList();
            using var dialog = new ExistingIssuePickerDialog(candidates);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedIssueIds.Count == 0)
            {
                return;
            }

            var currentUserId = _session.CurrentUserContext.RequireUserId();
            foreach (var selectedIssueId in dialog.SelectedIssueIds)
            {
                await _session.Issues.UpdateParentAsync(selectedIssueId, _details.Issue.Id, currentUserId);
            }

            await ReloadDetailsAsync(false, _disposeCts.Token);
            ActivateTab(DetailsTab.ChildIssues);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            ErrorDialogService.Show(ex);
        }
    }

    private async Task CreateChildIssueAsync()
    {
        if (_details is null || _details.Issue.Type != IssueType.Epic)
        {
            return;
        }

        try
        {
            using var dialog = new IssueEditorForm(_session, _projectId, null, null, _details.Issue.Id, IssueType.Story);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            await ReloadDetailsAsync(false, _disposeCts.Token);
            ActivateTab(DetailsTab.ChildIssues);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            ErrorDialogService.Show(ex);
        }
    }
    private async Task BindStatusOptionsAsync(Issue issue)
    {
        var currentUserId = _session.CurrentUserContext.RequireUserId();
        var transitions = await _session.Workflows.GetAllowedTransitionsAsync(issue.Id, currentUserId);
        _statusOptions = transitions
            .Append(new WorkflowStatusOptionDto(issue.WorkflowStatusId, issue.WorkflowStatus.Name, issue.WorkflowStatus.Color, issue.WorkflowStatus.Category, issue.WorkflowStatus.DisplayOrder))
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .OrderBy(x => x.DisplayOrder)
            .ToList();
        _status.DataSource = _statusOptions.ToList();
        _status.SelectedValue = issue.WorkflowStatusId;
    }

    private static Color ParseStatusColor(string? value, StatusCategory category)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return ColorTranslator.FromHtml(value);
            }
        }
        catch
        {
        }

        return category switch
        {
            StatusCategory.Done => JiraTheme.StatusDone,
            StatusCategory.InProgress => JiraTheme.StatusInProgress,
            _ => JiraTheme.StatusTodo
        };
    }

    private static Color GetStatusTextColor(StatusCategory category) => category == StatusCategory.ToDo ? JiraTheme.TextPrimary : Color.White;
    private static Label CreateMetaBadge()
    {
        var label = JiraControlFactory.CreateLabel(string.Empty, true);
        label.AutoSize = true;
        label.Padding = new Padding(10, 4, 10, 4);
        label.Margin = new Padding(0, 0, 8, 0);
        return label;
    }

    private static void ApplyMetaBadge(Label label, string text, Color backColor, Color foreColor)
    {
        label.Text = text;
        label.BackColor = backColor;
        label.ForeColor = foreColor;
    }
    private void ApplyAssigneeSummary(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
        {
            _assignee.SetPerson("Chưa phân công", "+", true);
            return;
        }

        if (names.Count == 1)
        {
            _assignee.SetPerson(names[0], Initials(names[0]), true);
            return;
        }

        var initials = Initials(names[0]);
        var summaryInitials = string.IsNullOrWhiteSpace(initials) ? "A+" : $"{initials[0]}+";
        _assignee.SetPerson($"{names[0]} +{names.Count - 1}", summaryInitials, true);
    }

    private void ConfigureWatchField()
    {
        _watchButton.AutoSize = false;
        _watchButton.Size = new Size(118, 34);
        _watchButton.Image = JiraIcons.GetEyeIcon(JiraTheme.PrimaryActive);
        _watchButton.ImageAlign = ContentAlignment.MiddleLeft;
        _watchButton.TextImageRelation = TextImageRelation.ImageBeforeText;
        _watchButton.Padding = new Padding(10, 0, 10, 0);

        _watcherCountLabel.Dock = DockStyle.Top;
        _watcherCountLabel.Height = 18;
        _watcherCountLabel.ForeColor = JiraTheme.TextSecondary;
        _watcherCountLabel.Font = JiraTheme.FontCaption;
        _watcherCountLabel.Margin = new Padding(0);
        _watcherAvatars.Dock = DockStyle.Fill;
        _watcherAvatars.Height = 28;
        _watcherAvatars.WrapContents = false;
        _watcherAvatars.Margin = new Padding(0);
        _watchersField.Controls.Add(_watcherAvatars);
        _watchersField.Controls.Add(_watcherCountLabel);
    }

    private void RenderWatchers()
    {
        _watcherCountLabel.Text = _watchers.Count switch
        {
            0 => "Không có người theo dõi",
            1 => "1 người theo dõi",
            _ => $"{_watchers.Count} người theo dõi"
        };

        _watcherAvatars.SuspendLayout();
        try
        {
            foreach (Control control in _watcherAvatars.Controls)
            {
                control.Dispose();
            }

            _watcherAvatars.Controls.Clear();
            foreach (var watcher in _watchers.Take(4))
            {
                _watcherAvatars.Controls.Add(new TinyAvatar(Initials(watcher.DisplayName), 24) { Margin = new Padding(0, 0, 6, 0) });
            }

            if (_watchers.Count > 4)
            {
                _watcherAvatars.Controls.Add(new TinyAvatar($"+{_watchers.Count - 4}", 24) { Margin = new Padding(0, 0, 6, 0), BackCircleColor = JiraTheme.Neutral500 });
            }
        }
        finally
        {
            _watcherAvatars.ResumeLayout();
        }

        _watchButton.Text = _isWatching ? "Đang theo dõi" : "Theo dõi";
        _watchButton.BackColor = _isWatching ? JiraTheme.Blue100 : JiraTheme.BgSurface;
        _watchButton.ForeColor = _isWatching ? JiraTheme.PrimaryActive : JiraTheme.TextPrimary;
        _watchButton.FlatAppearance.BorderColor = _isWatching ? JiraTheme.Primary : JiraTheme.Border;
    }

    private async Task ToggleWatchAsync()
    {
        if (_details is null)
        {
            return;
        }

        try
        {
            var currentUserId = _session.CurrentUserContext.RequireUserId();
            if (_isWatching)
            {
                await _session.Watchers.UnwatchIssueAsync(_issueId, currentUserId);
            }
            else
            {
                await _session.Watchers.WatchIssueAsync(_issueId, currentUserId);
            }

            _watchers = await _session.Watchers.GetWatchersAsync(_issueId, _disposeCts.Token);
            _isWatching = await _session.Watchers.IsWatchingAsync(_issueId, currentUserId, _disposeCts.Token);
            RenderWatchers();
        }
        catch (Exception ex)
        {
            ErrorDialogService.Show(ex);
        }
    }
    private void ConfigureDueDateField()
    {
        _dueDateDisplay.LinkColor = JiraTheme.TextSecondary;
        _dueDateDisplay.ActiveLinkColor = JiraTheme.PrimaryActive;
        _dueDateDisplay.VisitedLinkColor = JiraTheme.TextSecondary;
        _dueDateDisplay.Margin = new Padding(0);
        _dueDateDisplay.Padding = new Padding(0, 4, 0, 0);
        _dueDateField.Controls.Add(_dueDateDisplay);
        _dueDateField.Controls.Add(_dueDatePicker);
    }

    private void BeginDueDateEdit()
    {
        if (_loading || _details is null)
        {
            return;
        }

        _dueDateEditing = true;
        _dueDateDisplay.Visible = false;
        _dueDatePicker.Visible = true;
        _dueDatePicker.BringToFront();
        _dueDatePicker.Focus();
    }

    private void CancelDueDateEdit()
    {
        _dueDateEditing = false;
        _dueDatePicker.Visible = false;
        _dueDateDisplay.Visible = true;
    }

    private async Task CommitDueDateAsync()
    {
        if (!_dueDateEditing || _details is null)
        {
            return;
        }

        DateOnly? nextDueDate = _dueDatePicker.Checked ? DateOnly.FromDateTime(_dueDatePicker.Value.Date) : null;
        CancelDueDateEdit();

        if (_details.Issue.DueDate == nextDueDate)
        {
            BindDueDate(_details.Issue);
            return;
        }

        try
        {
            var currentUserId = _session.CurrentUserContext.RequireUserId();
            var updatedIssue = await _session.Issues.UpdateDueDateAsync(_issueId, nextDueDate, currentUserId);
            if (updatedIssue is null)
            {
                return;
            }

            var activityLogs = await _session.ActivityLog.GetIssueActivityAsync(_issueId);
            _loading = true;
            try
            {
                _details = _details with { Issue = updatedIssue, ActivityLogs = activityLogs };
                BindIssue(_details);
                _history.Bind(activityLogs);
                DialogResult = DialogResult.OK;
            }
            finally
            {
                _loading = false;
            }
        }
        catch (Exception ex)
        {
            ErrorDialogService.Show(ex);
            BindDueDate(_details.Issue);
        }
    }

    private void BindDueDate(Issue issue)
    {
        var overdue = issue.DueDate.HasValue
            && issue.DueDate.Value < DateOnly.FromDateTime(DateTime.Today)
            && (issue.WorkflowStatus?.Category ?? StatusCategory.ToDo) != StatusCategory.Done;
        var displayColor = issue.DueDate.HasValue
            ? overdue ? JiraTheme.Red600 : JiraTheme.TextPrimary
            : JiraTheme.TextSecondary;

        _dueDateDisplay.Text = issue.DueDate switch
        {
            DateOnly dueDate when overdue => $"Quá hạn {dueDate:dd MMM yyyy}",
            DateOnly dueDate => dueDate.ToString("dd MMM yyyy"),
            _ => "Không có hạn chót"
        };
        _dueDateDisplay.LinkColor = displayColor;
        _dueDateDisplay.ActiveLinkColor = displayColor;
        _dueDateDisplay.VisitedLinkColor = displayColor;
        _dueDateDisplay.ForeColor = displayColor;
        _dueDatePicker.Value = issue.DueDate?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
        _dueDatePicker.Checked = issue.DueDate.HasValue;
        CancelDueDateEdit();
    }

    private void BindMetadataOptions()
    {
        _component.DataSource = BuildLookupOptions(_availableComponents.Select(x => new LookupOption(x.Id, x.Name)).ToList(), "Không có thành phần");
        _fixVersion.DataSource = BuildLookupOptions(_availableVersions.Select(x => new LookupOption(x.Id, x.IsReleased ? $"{x.Name} (Đã phát hành)" : x.Name)).ToList(), "Không có phiên bản sửa lỗi");
    }

    private void RenderLabelChips()
    {
        _labelsField.SuspendLayout();
        try
        {
            _labelsField.Controls.Clear();
            var selectedLabels = _availableLabels.Where(x => _selectedLabelIds.Contains(x.Id)).OrderBy(x => x.Name).ToList();
            if (selectedLabels.Count == 0)
            {
                var empty = JiraControlFactory.CreateLabel("Không có nhãn", true);
                empty.Margin = new Padding(0, 4, 8, 4);
                _labelsField.Controls.Add(empty);
            }
            else
            {
                foreach (var label in selectedLabels)
                {
                    _labelsField.Controls.Add(new LabelChipControl
                    {
                        ChipText = label.Name,
                        ColorHex = label.Color,
                        Margin = new Padding(0, 0, 6, 6)
                    });
                }
            }

            _labelsField.Controls.Add(_editLabels);
        }
        finally
        {
            _labelsField.ResumeLayout();
        }
    }

    private static void SelectLookupValue(ComboBox comboBox, int? value)
    {
        if (comboBox.DataSource is not IEnumerable<LookupOption> options)
        {
            return;
        }

        var target = options.FirstOrDefault(x => x.Value == value) ?? options.First();
        comboBox.SelectedItem = target;
    }

    private static List<LookupOption> BuildLookupOptions(List<LookupOption> options, string emptyText)
    {
        options.Insert(0, new LookupOption(null, emptyText));
        return options;
    }
    private void BeginTitleEdit()
    {
        _titleEditor.Text = _title.Text;
        _title.Visible = false;
        _titleEditor.Visible = true;
        _titleEditor.Focus();
        _titleEditor.SelectAll();
    }

    private async Task CommitTitleAsync()
    {
        if (!_titleEditor.Visible) return;
        _title.Visible = true;
        _titleEditor.Visible = false;
        if (_details is null) return;
        var value = _titleEditor.Text.Trim();
        if (string.IsNullOrWhiteSpace(value) || value == _details.Issue.Title) { _titleEditor.Text = _details.Issue.Title; return; }
        _title.Text = value;
        await SaveIssueAsync();
    }

    private void CancelTitleEdit()
    {
        if (_details is null) return;
        _titleEditor.Text = _details.Issue.Title;
        _titleEditor.Visible = false;
        _title.Visible = true;
    }

    private void BindDescription(string? markdown)
    {
        _descriptionMarkdown = markdown ?? string.Empty;
        _descriptionEditor.SetContent(_descriptionMarkdown);
        _descriptionViewer.SetContent(_descriptionMarkdown);
        SetDescriptionMode(_descriptionPreviewMode);
    }

    private async Task ShowDescriptionPreviewAsync()
    {
        CaptureDescriptionMarkdown();
        SetDescriptionMode(true);
        await SaveDescriptionAsync();
    }

    private async Task SaveDescriptionAsync()
    {
        if (_loading || _details is null)
        {
            return;
        }

        CaptureDescriptionMarkdown();
        var nextDescription = NormalizeDescription(_descriptionMarkdown);
        var currentDescription = NormalizeDescription(_details.Issue.DescriptionText);
        if (string.Equals(nextDescription, currentDescription, StringComparison.Ordinal))
        {
            return;
        }

        await SaveIssueAsync();
    }

    private void CaptureDescriptionMarkdown()
    {
        if (_descriptionPreviewMode)
        {
            return;
        }

        _descriptionMarkdown = _descriptionEditor.GetContent();
        _descriptionViewer.SetContent(_descriptionMarkdown);
    }

    private void SetDescriptionMode(bool previewMode, bool focusEditor = false)
    {
        _descriptionPreviewMode = previewMode;
        _descriptionEditor.Visible = !previewMode;
        _descriptionViewer.Visible = previewMode;
        _descriptionEditor.SetReadOnly(previewMode);
        _descriptionViewer.SetContent(_descriptionMarkdown);
        ApplyModeButtonStyle(_descriptionEdit, !previewMode);
        ApplyModeButtonStyle(_descriptionPreview, previewMode);

        if (!previewMode && focusEditor)
        {
            _descriptionEditor.FocusEditor();
        }
    }

    private enum DetailsTab
    {
        Comments,
        History,
        ChildIssues
    }

    private static string? NormalizeDescription(string? markdown)
    {
        return string.IsNullOrWhiteSpace(markdown)
            ? null
            : markdown.Trim();
    }

    private void ActivateTab(DetailsTab tab)
    {
        var comments = tab == DetailsTab.Comments;
        var history = tab == DetailsTab.History;
        var childIssues = tab == DetailsTab.ChildIssues && _childIssuesTab.Visible;
        _comments.Visible = comments;
        _history.Visible = history;
        _childIssuesView.Visible = childIssues;
        _commentComposer.Visible = comments;
        _commentsTab.ForeColor = comments ? JiraTheme.TextPrimary : JiraTheme.TextSecondary;
        _historyTab.ForeColor = history ? JiraTheme.TextPrimary : JiraTheme.TextSecondary;
        _childIssuesTab.ForeColor = childIssues ? JiraTheme.TextPrimary : JiraTheme.TextSecondary;

        var activeTab = childIssues ? _childIssuesTab : history ? _historyTab : _commentsTab;
        _tabIndicator.Left = activeTab.Left;
        _tabIndicator.Width = activeTab.Width;
    }

    private async Task SaveIssueAsync()
    {
        if (_loading || _details is null) return;
        try
        {
            var currentUserId = _session.CurrentUserContext.RequireUserId();
            CaptureDescriptionMarkdown();
            var model = new IssueEditModel
            {
                Id = _details.Issue.Id,
                ProjectId = _details.Issue.ProjectId,
                Title = _titleEditor.Visible ? _titleEditor.Text.Trim() : _title.Text.Trim(),
                DescriptionText = NormalizeDescription(_descriptionMarkdown),
                Type = _details.Issue.Type,
                WorkflowStatusId = _status.SelectedValue is int statusId ? statusId : _details.Issue.WorkflowStatusId,
                Priority = _priority.SelectedItem is IssuePriority p ? p : _details.Issue.Priority,
                ReporterId = _details.Issue.ReporterId,
                CreatedById = currentUserId,
                EstimateHours = _details.Issue.EstimateHours,
                TimeSpentHours = _time.LoggedHours,
                TimeRemainingHours = _time.RemainingHours,
                StoryPoints = (int)_storyPoints.Value,
                DueDate = _details.Issue.DueDate,
                SprintId = _sprint.SelectedValue is int sprintId ? sprintId : null,
                ParentIssueId = _details.Issue.ParentIssueId,
                AssigneeIds = _selectedAssigneeIds.ToArray()
            };

            var updatedIssue = await _session.Issues.UpdateAsync(model);
            if (updatedIssue is null)
            {
                return;
            }

            var activityLogs = await _session.ActivityLog.GetIssueActivityAsync(_issueId);
            _loading = true;
            try
            {
                _details = _details with { Issue = updatedIssue, ActivityLogs = activityLogs };
                BindIssue(_details);
                _history.Bind(activityLogs);
                DialogResult = DialogResult.OK;
            }
            finally
            {
                _loading = false;
            }
        }
        catch (Exception ex) { ErrorDialogService.Show(ex); }
    }

    private async Task ShowAssigneePickerAsync()
    {
        if (_details is null) return;
        using var dialog = new AssigneePickerDialog(_users, _selectedAssigneeIds);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _selectedAssigneeIds = dialog.SelectedUserIds.ToHashSet();
        var selectedNames = _users.Where(x => _selectedAssigneeIds.Contains(x.Id)).Select(x => x.DisplayName).ToList();
        ApplyAssigneeSummary(selectedNames);
        await SaveIssueAsync();
    }

    private async Task ShowLabelPickerAsync()
    {
        if (_details is null)
        {
            return;
        }

        using var dialog = new LabelPickerDialog(_availableLabels, _selectedLabelIds);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            await _session.Labels.AssignToIssueAsync(_issueId, dialog.SelectedLabelIds);
            _selectedLabelIds = dialog.SelectedLabelIds.ToHashSet();
            RenderLabelChips();
            await ReloadDetailsAsync(false, _disposeCts.Token);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            ErrorDialogService.Show(ex);
        }
    }

    private async Task SaveComponentAsync()
    {
        if (_loading || _details is null)
        {
            return;
        }

        var selectedComponentId = _component.SelectedValue is int componentId ? (int?)componentId : null;
        var currentComponentId = _details.Issue.IssueComponents.Select(x => (int?)x.ComponentId).FirstOrDefault();
        if (selectedComponentId == currentComponentId)
        {
            return;
        }

        try
        {
            await _session.Components.AssignToIssueAsync(_issueId, selectedComponentId);
            await ReloadDetailsAsync(false, _disposeCts.Token);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            ErrorDialogService.Show(ex);
        }
    }

    private async Task SaveFixVersionAsync()
    {
        if (_loading || _details is null)
        {
            return;
        }

        var selectedVersionId = _fixVersion.SelectedValue is int versionId ? (int?)versionId : null;
        if (selectedVersionId == _details.Issue.FixVersionId)
        {
            return;
        }

        try
        {
            await _session.Versions.AssignToIssueAsync(_issueId, selectedVersionId);
            await ReloadDetailsAsync(false, _disposeCts.Token);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            ErrorDialogService.Show(ex);
        }
    }
    private async Task AddCommentAsync()
    {
        var body = _commentInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(body)) return;
        try
        {
            await _session.Comments.AddAsync(_issueId, _session.CurrentUserContext.RequireUserId(), _projectId, body);
            _commentInput.Clear();
            await ReloadDetailsAsync(false, _disposeCts.Token);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) { ErrorDialogService.Show(ex); }
    }

    private async Task EditCommentAsync(Comment comment)
    {
        using var editDialog = new CommentEditDialog("Sửa bình luận", comment.Body);
        if (editDialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(editDialog.Body)) return;
        try
        {
            await _session.Comments.UpdateAsync(comment.Id, _session.CurrentUserContext.RequireUserId(), editDialog.Body);
            await ReloadDetailsAsync(false, _disposeCts.Token);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) { ErrorDialogService.Show(ex); }
    }

    private async Task DeleteCommentAsync(Comment comment)
    {
        if (MessageBox.Show(this, "Xóa bình luận này?", "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            await _session.Comments.SoftDeleteAsync(comment.Id, _session.CurrentUserContext.RequireUserId());
            await ReloadDetailsAsync(false, _disposeCts.Token);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) { ErrorDialogService.Show(ex); }
    }

    private async Task UploadAttachmentAsync(string path)
    {
        try
        {
            await _session.Attachments.AddAsync(_issueId, _projectId, _session.CurrentUserContext.RequireUserId(), path);
            await ReloadDetailsAsync(false, _disposeCts.Token);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) { ErrorDialogService.Show(ex); }
    }

    private async Task DownloadAttachmentAsync(Attachment attachment)
    {
        try
        {
            var source = await _session.Attachments.ResolveDownloadPathAsync(attachment.Id);
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source)) { ErrorDialogService.Show("Không tìm thấy tệp đính kèm."); return; }
            using var dialog = new SaveFileDialog { FileName = attachment.OriginalFileName, RestoreDirectory = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            await using var input = File.OpenRead(source);
            await using var output = File.Create(dialog.FileName);
            await input.CopyToAsync(output);
            Process.Start(new ProcessStartInfo { FileName = dialog.FileName, UseShellExecute = true });
        }
        catch (Exception ex) { ErrorDialogService.Show(ex); }
    }

    private async Task DeleteAttachmentAsync(Attachment attachment)
    {
        if (MessageBox.Show(this, $"Xóa tệp đính kèm '{attachment.OriginalFileName}'?", "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            await _session.Attachments.SoftDeleteAsync(attachment.Id, _session.CurrentUserContext.RequireUserId(), _projectId);
            await ReloadDetailsAsync(false, _disposeCts.Token);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) { ErrorDialogService.Show(ex); }
    }

    private async Task LogTimeAsync()
    {
        if (_details is null) return;
        using var dialog = new LogTimeDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _time.LoggedHours += dialog.Hours;
            if (_time.EstimatedHours > 0) _time.RemainingHours = Math.Max(0, _time.EstimatedHours - _time.LoggedHours);
            await SaveIssueAsync();
            if (!string.IsNullOrWhiteSpace(dialog.Comment))
            {
                await _session.Comments.AddAsync(_issueId, _session.CurrentUserContext.RequireUserId(), _projectId, $"đã ghi {dialog.Hours}h: {dialog.Comment.Trim()}");
            }
            await ReloadDetailsAsync(false, _disposeCts.Token);
        }
        catch (Exception ex) { ErrorDialogService.Show(ex); }
    }

    private async Task DeleteIssueAsync()
    {
        if (MessageBox.Show(this, "Xóa issue này?", "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            if (!await _session.Issues.DeleteAsync(_issueId)) { ErrorDialogService.Show("Không thể xóa issue."); return; }
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) { ErrorDialogService.Show(ex); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelPendingLoad();
            _disposeCts.Cancel();
            Load -= OnIssueDetailsLoad;
            Shown -= OnIssueDetailsShown;
            Resize -= OnIssueDetailsResize;
            _split.SizeChanged -= OnSplitSizeChanged;
            _title.Click -= OnTitleClicked;
            _titleEditor.KeyDown -= OnTitleEditorKeyDown;
            _titleEditor.Leave -= OnTitleEditorLeave;
            _descriptionEdit.Click -= OnDescriptionEditClick;
            _descriptionPreview.Click -= OnDescriptionPreviewClick;
            _descriptionEditor.EditorLeave -= OnDescriptionEditorLeave;
            _saveComment.Click -= OnSaveCommentClick;
            _commentsTab.Click -= OnCommentsTabClick;
            _historyTab.Click -= OnHistoryTabClick;
            _childIssuesTab.Click -= OnChildIssuesTabClick;
            _addExistingIssueButton.Click -= OnAddExistingIssueButtonClick;
            _createChildIssueButton.Click -= OnCreateChildIssueButtonClick;
            _parentLink.LinkClicked -= OnParentLinkClicked;
            _status.DrawItem -= DrawStatus;
            _status.SelectedIndexChanged -= OnStatusSelectedIndexChanged;
            _priority.DrawItem -= DrawPriority;
            _priority.SelectedIndexChanged -= OnPrioritySelectedIndexChanged;
            _sprint.SelectedIndexChanged -= OnSprintSelectedIndexChanged;
            _storyPoints.ValueChanged -= OnStoryPointsValueChanged;
            _assignee.Click -= OnAssigneeClick;
            _editLabels.Click -= OnEditLabelsClick;
            _component.SelectedIndexChanged -= OnComponentSelectedIndexChanged;
            _fixVersion.SelectedIndexChanged -= OnFixVersionSelectedIndexChanged;
            _dueDateDisplay.LinkClicked -= OnDueDateDisplayLinkClicked;
            _dueDateDisplay.Click -= OnDueDateDisplayClick;
            _dueDatePicker.CloseUp -= OnDueDatePickerCloseUp;
            _dueDatePicker.Leave -= OnDueDatePickerLeave;
            _dueDatePicker.KeyDown -= OnDueDatePickerKeyDown;
            _watchButton.Click -= OnWatchButtonClick;
            _logTime.Click -= OnLogTimeClick;
            _delete.Click -= OnDeleteButtonClick;
            _deleteIssueMenuItem.Click -= OnDeleteIssueMenuItemClick;
            ContextMenuStrip?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void DrawStatus(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        e.DrawBackground();
        var value = (WorkflowStatusOptionDto)_status.Items[e.Index]!;
        DrawChip(e, IssueDisplayText.TranslateStatus(value.Name), ParseStatusColor(value.Color, value.Category), GetStatusTextColor(value.Category));
    }

    private void DrawPriority(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        e.DrawBackground();
        var value = (IssuePriority)_priority.Items[e.Index]!;
        var color = value switch
        {
            IssuePriority.Low => JiraTheme.Success,
            IssuePriority.Medium => JiraTheme.Warning,
            IssuePriority.High or IssuePriority.Highest => JiraTheme.Danger,
            _ => JiraTheme.Border
        };
        DrawChip(e, TranslateIssuePriority(value), color, value == IssuePriority.Medium ? JiraTheme.TextPrimary : Color.White);
    }

    private static void DrawChip(DrawItemEventArgs e, string text, Color bg, Color fg)
    {
        var bounds = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 5, e.Bounds.Width - 12, e.Bounds.Height - 10);
        using var path = GraphicsHelper.CreateRoundedPath(bounds, 10);
        using var fill = new SolidBrush(bg);
        e.Graphics.FillPath(fill, path);
        TextRenderer.DrawText(e.Graphics, text, JiraTheme.FontCaption, bounds, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        e.DrawFocusRectangle();
    }

    private static ComboBox CreateValueCombo()
    {
        return new ComboBox
        {
            DrawMode = DrawMode.Normal,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 220,
            FlatStyle = FlatStyle.Flat,
            BackColor = JiraTheme.BgSurface,
            ForeColor = JiraTheme.TextPrimary,
            Font = JiraTheme.FontBody
        };
    }
    private static Button CreateModeButton(string text)
    {
        return new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            AutoSize = false,
            Height = 32,
            BackColor = JiraTheme.BgSurface,
            ForeColor = JiraTheme.TextSecondary,
            Font = JiraTheme.FontSmall,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
    }

    private static void ApplyModeButtonStyle(Button button, bool active)
    {
        button.BackColor = active ? JiraTheme.Blue100 : JiraTheme.BgSurface;
        button.ForeColor = active ? JiraTheme.PrimaryActive : JiraTheme.TextSecondary;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = active ? JiraTheme.Primary : JiraTheme.Border;
        button.FlatAppearance.MouseOverBackColor = active ? JiraTheme.Blue100 : JiraTheme.Neutral100;
        button.FlatAppearance.MouseDownBackColor = active ? JiraTheme.Blue100 : JiraTheme.Neutral200;
    }

    private static Button MakeTab(string text, bool active)
    {
        var button = new Button { Text = text, FlatStyle = FlatStyle.Flat, AutoSize = false, Width = text switch { "Bình luận" => 108, "Issue con" => 96, _ => 90 }, Height = 32, BackColor = JiraTheme.BgSurface, Font = JiraTheme.FontSmall };
        button.FlatAppearance.BorderSize = 0;
        button.ForeColor = active ? JiraTheme.TextPrimary : JiraTheme.TextSecondary;
        return button;
    }

    private static Label TopLabel(string text)
    {
        var label = JiraControlFactory.CreateLabel(text);
        label.Dock = DockStyle.Top;
        label.Font = JiraTheme.FontSmall;
        return label;
    }

    private static void AddField(TableLayoutPanel table, int row, string label, Control value)
    {
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var left = JiraControlFactory.CreateLabel(label, true);
        left.Anchor = AnchorStyles.Left;
        value.Anchor = AnchorStyles.Left;
        value.Margin = new Padding(0, 4, 0, 10);
        table.Controls.Add(left, 0, row);
        table.Controls.Add(value, 1, row);
    }

    private static string TranslateIssuePriority(IssuePriority issuePriority) => issuePriority switch
    {
        IssuePriority.Lowest => "Thấp nhất",
        IssuePriority.Low => "Thấp",
        IssuePriority.Medium => "Trung bình",
        IssuePriority.High => "Cao",
        IssuePriority.Highest => "Cao nhất",
        _ => issuePriority.ToString()
    };

    private static string Initials(string name)
    {
        var chars = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(2).Select(x => char.ToUpperInvariant(x[0])).ToArray();
        return chars.Length == 0 ? "N" : new string(chars);
    }

    private sealed class TinyAvatar : Control
    {
        public TinyAvatar(string initials, int size)
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Initials = initials;
            Size = new Size(size, size);
            BackColor = Color.Transparent;
            DoubleBuffered = true;
        }

        public string Initials { get; }
        public Color BackCircleColor { get; set; } = JiraTheme.Primary;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SetClip(new Rectangle(0, 0, Width, Height));
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var fill = new SolidBrush(BackCircleColor);
            e.Graphics.FillEllipse(fill, ClientRectangle);
            TextRenderer.DrawText(e.Graphics, Initials, JiraTheme.FontCaption, ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
    private sealed class AvatarValueControl : Control
    {
        private string _initials = "N";
        private string _name = string.Empty;
        private bool _clickable;
        public AvatarValueControl()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            DoubleBuffered = true;
            Height = 32;
            BackColor = Color.Transparent;
        }
        public string PersonName => _name;
        public void SetPerson(string name, string initials, bool clickable) { _name = name; _initials = initials; _clickable = clickable; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SetClip(new Rectangle(0, 0, Width, Height));
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var avatar = new Rectangle(0, 4, 24, 24);
            using var fill = new SolidBrush(JiraTheme.Primary);
            e.Graphics.FillEllipse(fill, avatar);
            TextRenderer.DrawText(e.Graphics, _initials, JiraTheme.FontCaption, avatar, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics, _name, JiraTheme.FontSmall, new Rectangle(32, 0, Width - 32, Height), _clickable ? JiraTheme.Primary : JiraTheme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }

    private sealed class TimeTrackingBar : Control
    {
        public int EstimatedHours { get; set; }
        public int LoggedHours { get; set; }
        public int RemainingHours { get; set; }
        public TimeTrackingBar() { DoubleBuffered = true; BackColor = JiraTheme.BgSurface; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SetClip(new Rectangle(0, 0, Width, Height));
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 8, Width - 1, 14);
            using var bg = GraphicsHelper.CreateRoundedPath(bounds, 7);
            using var fillBg = new SolidBrush(JiraTheme.BgPage);
            e.Graphics.FillPath(fillBg, bg);
            if (EstimatedHours > 0 && LoggedHours > 0)
            {
                var width = Math.Min(bounds.Width, (int)Math.Round(bounds.Width * (LoggedHours / (double)EstimatedHours)));
                using var progress = GraphicsHelper.CreateRoundedPath(new Rectangle(bounds.X, bounds.Y, Math.Max(8, width), bounds.Height), 7);
                using var fill = new SolidBrush(JiraTheme.Primary);
                e.Graphics.FillPath(fill, progress);
            }
            TextRenderer.DrawText(e.Graphics, $"{LoggedHours}h đã ghi / {EstimatedHours}h ước tính", JiraTheme.FontCaption, new Rectangle(0, 28, Width, 20), JiraTheme.TextSecondary, TextFormatFlags.Left);
        }
    }

    private sealed class LogTimeDialog : Form
    {
        private readonly NumericUpDown _hours = new() { Minimum = 1, Maximum = 100, Value = 1, Width = 120 };
        private readonly TextBox _comment = JiraControlFactory.CreateTextBox();
        public LogTimeDialog()
        {
            Text = "Ghi thời gian";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(380, 220);
            MinimumSize = new Size(380, 220);
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = JiraTheme.BgSurface;
            _comment.Multiline = true;
            _comment.Height = 80;
            _comment.Dock = DockStyle.Fill;
            var save = JiraControlFactory.CreatePrimaryButton("Lưu");
            var cancel = JiraControlFactory.CreateSecondaryButton("Hủy");

            void CloseWithResult(DialogResult result)
            {
                DialogResult = result;
                Close();
            }

            void OnSaveClick(object? sender, EventArgs e) => CloseWithResult(DialogResult.OK);
            void OnCancelClick(object? sender, EventArgs e) => CloseWithResult(DialogResult.Cancel);

            save.Click += OnSaveClick;
            cancel.Click += OnCancelClick;
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = JiraTheme.BgSurface };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(16), BackColor = JiraTheme.BgSurface };
            layout.Controls.Add(JiraControlFactory.CreateLabel("Số giờ", true), 0, 0);
            layout.Controls.Add(_hours, 0, 1);
            layout.Controls.Add(JiraControlFactory.CreateLabel("Bình luận", true), 0, 2);
            layout.Controls.Add(_comment, 0, 3);
            Controls.Add(layout);
            Controls.Add(buttons);
        }
        public int Hours => (int)_hours.Value;
        public string Comment => _comment.Text;
    }

    private sealed record LookupOption(int? Value, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed class LabelPickerDialog : Form
    {
        private readonly CheckedListBox _labels = new()
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            CheckOnClick = true,
            BackColor = JiraTheme.BgSurface,
            ForeColor = JiraTheme.TextPrimary,
            Font = JiraTheme.FontBody
        };

        public LabelPickerDialog(IReadOnlyList<JiraLabelEntity> labels, IEnumerable<int> selectedLabelIds)
        {
            Text = "Sửa nhãn";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(420, 420);
            MinimumSize = new Size(420, 420);
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = JiraTheme.BgSurface;

            var selected = selectedLabelIds.ToHashSet();
            foreach (var label in labels.OrderBy(x => x.Name))
            {
                _labels.Items.Add(label, selected.Contains(label.Id));
            }

            _labels.DisplayMember = nameof(JiraLabelEntity.Name);

            var save = JiraControlFactory.CreatePrimaryButton("Áp dụng");
            var cancel = JiraControlFactory.CreateSecondaryButton("Hủy");

            void CloseWithResult(DialogResult result)
            {
                DialogResult = result;
                Close();
            }

            void OnSaveClick(object? sender, EventArgs e) => CloseWithResult(DialogResult.OK);
            void OnCancelClick(object? sender, EventArgs e) => CloseWithResult(DialogResult.Cancel);

            save.Click += OnSaveClick;
            cancel.Click += OnCancelClick;

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(12),
                BackColor = JiraTheme.BgSurface
            };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);

            var caption = JiraControlFactory.CreateLabel("Chọn một hoặc nhiều nhãn", true);
            caption.Dock = DockStyle.Top;
            caption.Height = 28;

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                BackColor = JiraTheme.BgSurface
            };
            content.Controls.Add(_labels);
            content.Controls.Add(caption);

            Controls.Add(content);
            Controls.Add(buttons);
        }

        public IReadOnlyList<int> SelectedLabelIds => _labels.CheckedItems.Cast<JiraLabelEntity>().Select(x => x.Id).ToList();
    }
    private sealed record ExistingIssueOption(int Id, string DisplayText)
    {
        public override string ToString() => DisplayText;
    }

    private sealed class ExistingIssuePickerDialog : Form
    {
        private readonly CheckedListBox _issues = new()
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            CheckOnClick = true,
            BackColor = JiraTheme.BgSurface,
            ForeColor = JiraTheme.TextPrimary,
            Font = JiraTheme.FontBody
        };

        public ExistingIssuePickerDialog(IReadOnlyList<IssueSummaryDto> issues)
        {
            Text = "Thêm issue có sẵn";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(520, 460);
            MinimumSize = new Size(520, 460);
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = JiraTheme.BgSurface;

            foreach (var issue in issues)
            {
                var context = string.IsNullOrWhiteSpace(issue.EpicTitle) ? "Không có Epic" : issue.EpicTitle;
                _issues.Items.Add(new ExistingIssueOption(issue.Id, $"{issue.IssueKey} - {issue.Title} ({context})"), false);
            }

            _issues.DisplayMember = nameof(ExistingIssueOption.DisplayText);

            var save = JiraControlFactory.CreatePrimaryButton("Liên kết issue");
            var cancel = JiraControlFactory.CreateSecondaryButton("Hủy");

            void CloseWithResult(DialogResult result)
            {
                DialogResult = result;
                Close();
            }

            void OnSaveClick(object? sender, EventArgs e) => CloseWithResult(DialogResult.OK);
            void OnCancelClick(object? sender, EventArgs e) => CloseWithResult(DialogResult.Cancel);

            save.Click += OnSaveClick;
            cancel.Click += OnCancelClick;

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(12),
                BackColor = JiraTheme.BgSurface
            };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);

            var caption = JiraControlFactory.CreateLabel("Chọn một hoặc nhiều story hoặc task có sẵn", true);
            caption.Dock = DockStyle.Top;
            caption.Height = 28;

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                BackColor = JiraTheme.BgSurface
            };
            content.Controls.Add(_issues);
            content.Controls.Add(caption);

            Controls.Add(content);
            Controls.Add(buttons);
        }

        public IReadOnlyList<int> SelectedIssueIds => _issues.CheckedItems.Cast<ExistingIssueOption>().Select(x => x.Id).ToList();
    }
    private sealed class AssigneePickerDialog : Form
    {
        private readonly CheckedListBox _assignees = new()
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            CheckOnClick = true,
            BackColor = JiraTheme.BgSurface,
            ForeColor = JiraTheme.TextPrimary,
            Font = JiraTheme.FontBody
        };

        public AssigneePickerDialog(IReadOnlyList<User> users, IEnumerable<int> selectedUserIds)
        {
            Text = "Giao người dùng";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(420, 420);
            MinimumSize = new Size(420, 420);
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = JiraTheme.BgSurface;

            var selected = selectedUserIds.ToHashSet();
            foreach (var user in users.OrderBy(x => x.DisplayName))
            {
                _assignees.Items.Add(user, selected.Contains(user.Id));
            }

            _assignees.DisplayMember = nameof(User.DisplayName);

            var save = JiraControlFactory.CreatePrimaryButton("Áp dụng");
            var cancel = JiraControlFactory.CreateSecondaryButton("Hủy");

            void CloseWithResult(DialogResult result)
            {
                DialogResult = result;
                Close();
            }

            void OnSaveClick(object? sender, EventArgs e) => CloseWithResult(DialogResult.OK);
            void OnCancelClick(object? sender, EventArgs e) => CloseWithResult(DialogResult.Cancel);

            save.Click += OnSaveClick;
            cancel.Click += OnCancelClick;

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(12),
                BackColor = JiraTheme.BgSurface
            };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);

            var caption = JiraControlFactory.CreateLabel("Chọn một hoặc nhiều người được giao", true);
            caption.Dock = DockStyle.Top;
            caption.Height = 28;

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                BackColor = JiraTheme.BgSurface
            };
            content.Controls.Add(_assignees);
            content.Controls.Add(caption);

            Controls.Add(content);
            Controls.Add(buttons);
        }

        public IReadOnlyList<int> SelectedUserIds => _assignees.CheckedItems.Cast<User>().Select(x => x.Id).ToList();
    }
}

































































































