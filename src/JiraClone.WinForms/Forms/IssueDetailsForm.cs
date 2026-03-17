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
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public class IssueDetailsForm : Form
{
    private const int DescriptionSectionHeight = 290;
    private const int PreferredLeftPanelMinWidth = 420;
    private const int PreferredRightPanelMinWidth = 260;
    private readonly AppSession _session;
    private readonly int _issueId;
    private readonly int _projectId;
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
    private readonly Button _descriptionEdit = CreateModeButton("Edit");
    private readonly Button _descriptionPreview = CreateModeButton("Preview");
    private readonly AttachmentPicker _attachmentPicker = new() { Dock = DockStyle.Top, Height = 42 };
    private readonly AttachmentListControl _attachments = new() { Dock = DockStyle.Fill };
    private readonly Button _commentsTab = MakeTab("Comments", true);
    private readonly Button _historyTab = MakeTab("History", false);
    private readonly Panel _tabIndicator = new() { Height = 2, Width = 100, BackColor = JiraTheme.Primary };
    private readonly TextBox _commentInput = JiraControlFactory.CreateTextBox();
    private readonly Button _saveComment = JiraControlFactory.CreatePrimaryButton("Save");
    private readonly CommentListControl _comments = new() { Dock = DockStyle.Fill };
    private readonly ActivityTimelineControl _history = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly ComboBox _status = new() { DrawMode = DrawMode.OwnerDrawFixed, DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly ComboBox _priority = new() { DrawMode = DrawMode.OwnerDrawFixed, DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly SprintSelectorControl _sprint = new() { Width = 180 };
    private readonly NumericUpDown _storyPoints = new() { Width = 180, Minimum = 0, Maximum = 100, BorderStyle = BorderStyle.FixedSingle };
    private readonly TimeTrackingBar _time = new() { Dock = DockStyle.Top, Height = 52 };
    private readonly Button _logTime = JiraControlFactory.CreateSecondaryButton("Log Time");
    private readonly AvatarValueControl _assignee = new() { Width = 220, Cursor = Cursors.Hand };
    private readonly AvatarValueControl _reporter = new() { Width = 220 };
    private readonly FlowLayoutPanel _labelsField = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, MaximumSize = new Size(220, 0), Margin = new Padding(0), BackColor = Color.Transparent };
    private readonly Button _editLabels = JiraControlFactory.CreateSecondaryButton("Edit Labels");
    private readonly ComboBox _component = CreateValueCombo();
    private readonly ComboBox _fixVersion = CreateValueCombo();
    private readonly Button _delete = JiraControlFactory.CreateSecondaryButton("Delete");
    private readonly LinkLabel _parentLink = new() { AutoSize = true, Font = JiraTheme.FontBody, LinkColor = JiraTheme.PrimaryActive, Visible = false };
    private readonly Panel _parentSection = new() { Dock = DockStyle.Top, Height = 40, Visible = false, BackColor = JiraTheme.BgSurface };
    private readonly FlowLayoutPanel _childIssuesPanel = new() { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Visible = false, BackColor = JiraTheme.BgSurface, Padding = new Padding(0, 4, 0, 4) };
    private readonly Panel _childSection = new() { Dock = DockStyle.Top, AutoSize = true, Visible = false, BackColor = JiraTheme.BgSurface };
    private IssueDetailsDto? _details;
    private IReadOnlyList<User> _users = Array.Empty<User>();
    private IReadOnlyList<JiraLabelEntity> _availableLabels = Array.Empty<JiraLabelEntity>();
    private IReadOnlyList<JiraComponentEntity> _availableComponents = Array.Empty<JiraComponentEntity>();
    private IReadOnlyList<JiraProjectVersionEntity> _availableVersions = Array.Empty<JiraProjectVersionEntity>();
    private HashSet<int> _selectedAssigneeIds = [];
    private HashSet<int> _selectedLabelIds = [];
    private bool _loading;
    private string _descriptionMarkdown = string.Empty;
    private bool _descriptionPreviewMode;
    private bool _descriptionModeInitialized;

    public IssueDetailsForm(AppSession session, int issueId, int projectId)
    {
        _session = session;
        _issueId = issueId;
        _projectId = projectId;
        Text = "Issue Details";
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(900, 600);
        Size = new Size(1180, 760);
        BackColor = JiraTheme.BgPage;
        DoubleBuffered = true;
        _split.FixedPanel = FixedPanel.Panel2;
        _split.Panel1MinSize = PreferredLeftPanelMinWidth;
        _split.Panel2MinSize = PreferredRightPanelMinWidth;

        _split.Panel1.BackColor = JiraTheme.BgSurface;
        _split.Panel2.BackColor = JiraTheme.BgSurface;
        Controls.Add(_split);

        _title.Font = JiraTheme.FontH1;
        _title.AutoSize = false;
        _title.Height = 42;
        _title.Click += (_, _) => BeginTitleEdit();
        _titleEditor.Font = JiraTheme.FontH1;
        _titleEditor.Visible = false;
        _titleEditor.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await CommitTitleAsync(); }
            else if (e.KeyCode == Keys.Escape) CancelTitleEdit();
        };
        _titleEditor.Leave += async (_, _) => await CommitTitleAsync();

        _descriptionEdit.Click += (_, _) => SetDescriptionMode(false, true);
        _descriptionPreview.Click += async (_, _) => await ShowDescriptionPreviewAsync();
        _descriptionEditor.EditorLeave += async (_, _) => await SaveDescriptionAsync();

        _commentInput.Multiline = true;
        _commentInput.Height = 72;
        _saveComment.AutoSize = false;
        _saveComment.Size = new Size(90, 36);
        _saveComment.Click += async (_, _) => await AddCommentAsync();

        _status.DataSource = Enum.GetValues<IssueStatus>();
        _status.DrawItem += DrawStatus;
        _status.SelectedIndexChanged += async (_, _) => await SaveIssueAsync();
        _priority.DataSource = Enum.GetValues<IssuePriority>();
        _priority.DrawItem += DrawPriority;
        _priority.SelectedIndexChanged += async (_, _) => await SaveIssueAsync();
        _sprint.SelectedIndexChanged += async (_, _) => await SaveIssueAsync();
        _storyPoints.ValueChanged += async (_, _) => { if (_storyPoints.Focused) await SaveIssueAsync(); };
        _assignee.Click += async (_, _) => await ShowAssigneePickerAsync();
        _editLabels.AutoSize = false;
        _editLabels.Size = new Size(96, 28);
        _editLabels.Margin = new Padding(0, 0, 0, 0);
        _editLabels.Click += async (_, _) => await ShowLabelPickerAsync();
        _component.DisplayMember = nameof(LookupOption.Text);
        _component.ValueMember = nameof(LookupOption.Value);
        _component.SelectedIndexChanged += async (_, _) => await SaveComponentAsync();
        _fixVersion.DisplayMember = nameof(LookupOption.Text);
        _fixVersion.ValueMember = nameof(LookupOption.Value);
        _fixVersion.SelectedIndexChanged += async (_, _) => await SaveFixVersionAsync();
        _logTime.AutoSize = false;
        _logTime.Size = new Size(100, 36);
        _logTime.Click += async (_, _) => await LogTimeAsync();
        _delete.AutoSize = false;
        _delete.Size = new Size(88, 34);
        _delete.Click += async (_, _) => await DeleteIssueAsync();

        _comments.EditRequested = EditCommentAsync;
        _comments.DeleteRequested = DeleteCommentAsync;
        _attachments.DownloadRequested = DownloadAttachmentAsync;
        _attachments.DeleteRequested = DeleteAttachmentAsync;
        _attachmentPicker.UploadRequested = UploadAttachmentAsync;
        ContextMenuStrip = new ContextMenuStrip();
        ContextMenuStrip.Items.Add("Delete issue", null, async (_, _) => await DeleteIssueAsync());

        _split.Panel1.Controls.Add(BuildLeft());
        _split.Panel2.Controls.Add(BuildRight());
        Load += (_, _) => QueueSafeUpdateSplitLayout();
        Shown += async (_, _) =>
        {
            QueueSafeUpdateSplitLayout();
            await LoadDetailsAsync();
            QueueSafeUpdateSplitLayout();
        };
        Resize += (_, _) => QueueSafeUpdateSplitLayout();
        _split.SizeChanged += (_, _) => QueueSafeUpdateSplitLayout();
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

            if (_split.Panel1MinSize != leftMin)
            {
                _split.Panel1MinSize = leftMin;
            }

            if (_split.Panel2MinSize != rightMin)
            {
                _split.Panel2MinSize = rightMin;
            }

            var preferred = (int)Math.Round(totalWidth * 0.64);
            var maxPanel1 = totalWidth - rightMin;
            var minPanel1 = leftMin;
            var safeDistance = Math.Max(minPanel1, Math.Min(preferred, maxPanel1));

            if (safeDistance > 0 && safeDistance != _split.SplitterDistance)
            {
                _split.SplitterDistance = safeDistance;
            }
        }
        catch (ArgumentOutOfRangeException ex)
        {
            System.Diagnostics.Debug.WriteLine($"SplitLayout range error: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            System.Diagnostics.Debug.WriteLine($"SplitLayout operation error: {ex.Message}");
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

        var label = TopLabel("Description");
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
        host.Controls.Add(TopLabel("Attachments"));
        return host;
    }

    private Control BuildActivity()
    {
        var host = new Panel { Dock = DockStyle.Top, Height = 344, BackColor = JiraTheme.BgSurface, Padding = new Padding(0, 8, 0, 0) };
        var tabs = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = JiraTheme.BgSurface };
        _commentsTab.Location = new Point(0, 0);
        _historyTab.Location = new Point(104, 0);
        _commentsTab.Click += (_, _) => ActivateTab(true);
        _historyTab.Click += (_, _) => ActivateTab(false);
        _tabIndicator.Location = new Point(0, 32);
        tabs.Controls.Add(_tabIndicator);
        tabs.Controls.Add(_historyTab);
        tabs.Controls.Add(_commentsTab);

        var composer = new Panel { Dock = DockStyle.Top, Height = 116, BackColor = JiraTheme.BgSurface, Padding = new Padding(0, 8, 0, 8) };
        _commentInput.Dock = DockStyle.Fill;
        _saveComment.Dock = DockStyle.Bottom;
        _saveComment.Height = 36;
        composer.Controls.Add(_commentInput);
        composer.Controls.Add(_saveComment);

        var content = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
        content.Controls.Add(_comments);
        content.Controls.Add(_history);

        host.Controls.Add(content);
        host.Controls.Add(composer);
        host.Controls.Add(tabs);
        host.Controls.Add(TopLabel("Activity"));
        return host;
    }

    private Control BuildRight()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface, Padding = new Padding(20, 24, 20, 24), AutoScroll = true };
        var detailsLabel = JiraControlFactory.CreateLabel("Details", true);
        detailsLabel.Dock = DockStyle.Top;
        detailsLabel.Font = JiraTheme.FontCaption;
        detailsLabel.Height = 24;

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = JiraTheme.BgSurface };
        _delete.Dock = DockStyle.Right;
        toolbar.Controls.Add(_delete);

        var statusHost = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = JiraTheme.BgSurface, Padding = new Padding(0, 4, 0, 0) };
        _status.Dock = DockStyle.Left;
        statusHost.Controls.Add(_status);

        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, BackColor = JiraTheme.BgSurface, Padding = new Padding(0, 8, 0, 8) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddField(fields, 0, "Assignee", _assignee);
        AddField(fields, 1, "Reporter", _reporter);
        AddField(fields, 2, "Labels", _labelsField);
        AddField(fields, 3, "Component", _component);
        AddField(fields, 4, "Fix version", _fixVersion);
        AddField(fields, 5, "Priority", _priority);
        AddField(fields, 6, "Sprint", _sprint);
        AddField(fields, 7, "Story points", _storyPoints);

        var detailsBlock = new Panel { Dock = DockStyle.Top, Height = 418, BackColor = JiraTheme.BgSurface };
        detailsBlock.Controls.Add(fields);
        detailsBlock.Controls.Add(statusHost);
        detailsBlock.Controls.Add(TopLabel("Status"));

        var timeBlock = new Panel { Dock = DockStyle.Top, Height = 114, BackColor = JiraTheme.BgSurface, Padding = new Padding(0, 8, 0, 0) };
        _logTime.Dock = DockStyle.Top;
        _time.Dock = DockStyle.Top;
        timeBlock.Controls.Add(_logTime);
        timeBlock.Controls.Add(_time);

        // Parent section
        _parentLink.Dock = DockStyle.Fill;
        _parentSection.Controls.Add(_parentLink);
        var parentLabel = TopLabel("Parent");

        // Child issues section
        _childSection.Controls.Add(_childIssuesPanel);
        var childLabel = TopLabel("Child Issues");
        childLabel.Tag = "childLabel";

        panel.Controls.Add(_childSection);
        panel.Controls.Add(childLabel);
        panel.Controls.Add(JiraControlFactory.CreateSeparator());
        panel.Controls.Add(timeBlock);
        panel.Controls.Add(TopLabel("Time Tracking"));
        panel.Controls.Add(JiraControlFactory.CreateSeparator());
        panel.Controls.Add(_parentSection);
        panel.Controls.Add(parentLabel);
        panel.Controls.Add(JiraControlFactory.CreateSeparator());
        panel.Controls.Add(detailsBlock);
        panel.Controls.Add(detailsLabel);
        panel.Controls.Add(toolbar);
        return panel;
    }

    private async Task LoadDetailsAsync(bool refreshReferenceData = true)
    {
        try
        {
            _loading = true;
            if (refreshReferenceData || _users.Count == 0)
            {
                _users = await _session.Users.GetProjectUsersAsync(_projectId);
                _availableLabels = await _session.Labels.GetByProjectAsync(_projectId);
                _availableComponents = await _session.Components.GetByProjectAsync(_projectId);
                _availableVersions = await _session.Versions.GetByProjectAsync(_projectId);
                _sprint.Bind(await _session.Sprints.GetByProjectAsync(_projectId));
                BindMetadataOptions();
            }

            _details = await _session.Issues.GetDetailsAsync(_issueId);
            if (_details is null) { ErrorDialogService.Show("Issue not found."); Close(); return; }
            BindIssue(_details);
            _comments.Bind(_details.Comments);
            _attachments.Bind(_details.Attachments);
            _history.Bind(_details.ActivityLogs);
            ActivateTab(_comments.Visible || !_history.Visible);
        }
        catch (Exception ex) { ErrorDialogService.Show(ex); }
        finally { _loading = false; }
    }

    private void BindIssue(IssueDetailsDto details)
    {
        var issue = details.Issue;
        _breadcrumb.Text = issue.IssueKey;
        _title.Text = issue.Title;
        _titleEditor.Text = issue.Title;
        ApplyMetaBadge(_keyBadge, issue.IssueKey, JiraTheme.Blue100, JiraTheme.PrimaryActive);
        ApplyMetaBadge(_statusBadge, issue.Status.ToString(), issue.Status switch
        {
            IssueStatus.Done => JiraTheme.StatusDone,
            IssueStatus.InProgress => JiraTheme.StatusInProgress,
            _ => JiraTheme.StatusTodo
        }, issue.Status is IssueStatus.Done or IssueStatus.InProgress ? Color.White : JiraTheme.TextPrimary);
        ApplyMetaBadge(_priorityBadge, issue.Priority.ToString(), issue.Priority switch
        {
            IssuePriority.Low => JiraTheme.Success,
            IssuePriority.Medium => JiraTheme.Warning,
            IssuePriority.High or IssuePriority.Highest => JiraTheme.Danger,
            _ => JiraTheme.Border
        }, issue.Priority == IssuePriority.Medium ? JiraTheme.TextPrimary : Color.White);
        _updatedLabel.Text = $"Updated {issue.UpdatedAtUtc:g}";
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

        _status.SelectedItem = issue.Status;
        _priority.SelectedItem = issue.Priority;
        if (issue.SprintId.HasValue) _sprint.SelectedValue = issue.SprintId.Value; else _sprint.SelectedIndex = -1;
        _storyPoints.Value = Math.Max(_storyPoints.Minimum, Math.Min(_storyPoints.Maximum, issue.StoryPoints ?? 0));

        _selectedLabelIds = issue.IssueLabels.Select(x => x.LabelId).ToHashSet();
        RenderLabelChips();
        SelectLookupValue(_component, issue.IssueComponents.Select(x => (int?)x.ComponentId).FirstOrDefault());
        SelectLookupValue(_fixVersion, issue.FixVersionId);

        _selectedAssigneeIds = issue.Assignees.Select(x => x.UserId).ToHashSet();
        ApplyAssigneeSummary(issue.Assignees.Select(x => x.User.DisplayName).ToList());
        _reporter.SetPerson(issue.Reporter.DisplayName, Initials(issue.Reporter.DisplayName), false);
        _time.EstimatedHours = issue.EstimateHours ?? 0;
        _time.LoggedHours = issue.TimeSpentHours ?? 0;
        _time.RemainingHours = issue.TimeRemainingHours ?? Math.Max(0, _time.EstimatedHours - _time.LoggedHours);
        _time.Invalidate();

        // Parent link
        if (issue.ParentIssue is not null)
        {
            _parentLink.Text = $"{issue.ParentIssue.IssueKey} — {issue.ParentIssue.Title}";
            _parentLink.Links.Clear();
            _parentLink.Links.Add(0, issue.ParentIssue.IssueKey.Length, issue.ParentIssue.Id);
            _parentSection.Visible = true;
        }
        else
        {
            _parentSection.Visible = false;
        }

        // Child issues
        _childIssuesPanel.Controls.Clear();
        if (details.SubIssues.Count > 0)
        {
            foreach (var child in details.SubIssues)
            {
                var row = new Label
                {
                    Text = $"  {child.IssueKey}  {child.Title}  [{child.Status}]",
                    Font = JiraTheme.FontCaption,
                    ForeColor = JiraTheme.TextPrimary,
                    AutoSize = true,
                    Padding = new Padding(0, 2, 0, 2),
                    Cursor = Cursors.Hand
                };
                var childId = child.Id;
                row.Click += (_, _) => { using var f = new IssueDetailsForm(_session, childId, _projectId); f.ShowDialog(this); };
                _childIssuesPanel.Controls.Add(row);
            }
            _childSection.Visible = true;
        }
        else
        {
            _childSection.Visible = false;
        }
    }

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
            _assignee.SetPerson("Unassigned", "+", true);
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

    private void BindMetadataOptions()
    {
        _component.DataSource = BuildLookupOptions(_availableComponents.Select(x => new LookupOption(x.Id, x.Name)).ToList(), "No component");
        _fixVersion.DataSource = BuildLookupOptions(_availableVersions.Select(x => new LookupOption(x.Id, x.IsReleased ? $"{x.Name} (Released)" : x.Name)).ToList(), "No fix version");
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
                var empty = JiraControlFactory.CreateLabel("No labels", true);
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

    private static string? NormalizeDescription(string? markdown)
    {
        return string.IsNullOrWhiteSpace(markdown)
            ? null
            : markdown.Trim();
    }

    private void ActivateTab(bool comments)
    {
        _comments.Visible = comments;
        _history.Visible = !comments;
        _commentInput.Visible = comments;
        _saveComment.Visible = comments;
        _commentsTab.ForeColor = comments ? JiraTheme.TextPrimary : JiraTheme.TextSecondary;
        _historyTab.ForeColor = comments ? JiraTheme.TextSecondary : JiraTheme.TextPrimary;
        _tabIndicator.Left = comments ? _commentsTab.Left : _historyTab.Left;
        _tabIndicator.Width = comments ? _commentsTab.Width : _historyTab.Width;
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
                Status = _status.SelectedItem is IssueStatus s ? s : _details.Issue.Status,
                Priority = _priority.SelectedItem is IssuePriority p ? p : _details.Issue.Priority,
                ReporterId = _details.Issue.ReporterId,
                CreatedById = currentUserId,
                EstimateHours = _details.Issue.EstimateHours,
                TimeSpentHours = _time.LoggedHours,
                TimeRemainingHours = _time.RemainingHours,
                StoryPoints = (int)_storyPoints.Value,
                SprintId = _sprint.SelectedValue is int sprintId ? sprintId : null,
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
            await LoadDetailsAsync(false);
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
            await LoadDetailsAsync(false);
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
            await LoadDetailsAsync(false);
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
            await LoadDetailsAsync(false);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) { ErrorDialogService.Show(ex); }
    }

    private async Task EditCommentAsync(Comment comment)
    {
        using var editDialog = new CommentEditDialog("Edit Comment", comment.Body);
        if (editDialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(editDialog.Body)) return;
        try
        {
            await _session.Comments.UpdateAsync(comment.Id, _session.CurrentUserContext.RequireUserId(), editDialog.Body);
            await LoadDetailsAsync(false);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) { ErrorDialogService.Show(ex); }
    }

    private async Task DeleteCommentAsync(Comment comment)
    {
        if (MessageBox.Show(this, "Delete this comment?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            await _session.Comments.SoftDeleteAsync(comment.Id, _session.CurrentUserContext.RequireUserId());
            await LoadDetailsAsync(false);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) { ErrorDialogService.Show(ex); }
    }

    private async Task UploadAttachmentAsync(string path)
    {
        try
        {
            await _session.Attachments.AddAsync(_issueId, _projectId, _session.CurrentUserContext.RequireUserId(), path);
            await LoadDetailsAsync(false);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) { ErrorDialogService.Show(ex); }
    }

    private async Task DownloadAttachmentAsync(Attachment attachment)
    {
        try
        {
            var source = await _session.Attachments.ResolveDownloadPathAsync(attachment.Id);
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source)) { ErrorDialogService.Show("Attachment file was not found."); return; }
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
        if (MessageBox.Show(this, $"Delete attachment '{attachment.OriginalFileName}'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            await _session.Attachments.SoftDeleteAsync(attachment.Id, _session.CurrentUserContext.RequireUserId(), _projectId);
            await LoadDetailsAsync(false);
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
                await _session.Comments.AddAsync(_issueId, _session.CurrentUserContext.RequireUserId(), _projectId, $"Logged {dialog.Hours}h: {dialog.Comment.Trim()}");
            }
            await LoadDetailsAsync(false);
        }
        catch (Exception ex) { ErrorDialogService.Show(ex); }
    }

    private async Task DeleteIssueAsync()
    {
        if (MessageBox.Show(this, "Delete this issue?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            if (!await _session.Issues.DeleteAsync(_issueId)) { ErrorDialogService.Show("Issue could not be deleted."); return; }
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) { ErrorDialogService.Show(ex); }
    }

    private void DrawStatus(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        e.DrawBackground();
        var value = (IssueStatus)_status.Items[e.Index]!;
        DrawChip(e, value.ToString(), value switch
        {
            IssueStatus.Done => JiraTheme.StatusDone,
            IssueStatus.InProgress => JiraTheme.StatusInProgress,
            _ => JiraTheme.StatusTodo
        }, value is IssueStatus.Done or IssueStatus.InProgress ? Color.White : JiraTheme.TextPrimary);
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
        DrawChip(e, value.ToString(), color, value == IssuePriority.Medium ? JiraTheme.TextPrimary : Color.White);
    }

    private static void DrawChip(DrawItemEventArgs e, string text, Color bg, Color fg)
    {
        var bounds = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 5, e.Bounds.Width - 12, e.Bounds.Height - 10);
        using var path = Round(bounds, 10);
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
        var button = new Button { Text = text, FlatStyle = FlatStyle.Flat, AutoSize = false, Width = text == "Comments" ? 100 : 90, Height = 32, BackColor = JiraTheme.BgSurface, Font = JiraTheme.FontSmall };
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

    private static string Initials(string name)
    {
        var chars = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(2).Select(x => char.ToUpperInvariant(x[0])).ToArray();
        return chars.Length == 0 ? "U" : new string(chars);
    }

    private static GraphicsPath Round(Rectangle bounds, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class AvatarValueControl : Control
    {
        private string _initials = "U";
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
            using var bg = Round(bounds, 7);
            using var fillBg = new SolidBrush(JiraTheme.BgPage);
            e.Graphics.FillPath(fillBg, bg);
            if (EstimatedHours > 0 && LoggedHours > 0)
            {
                var width = Math.Min(bounds.Width, (int)Math.Round(bounds.Width * (LoggedHours / (double)EstimatedHours)));
                using var progress = Round(new Rectangle(bounds.X, bounds.Y, Math.Max(8, width), bounds.Height), 7);
                using var fill = new SolidBrush(JiraTheme.Primary);
                e.Graphics.FillPath(fill, progress);
            }
            TextRenderer.DrawText(e.Graphics, $"{LoggedHours}h logged / {EstimatedHours}h estimated", JiraTheme.FontCaption, new Rectangle(0, 28, Width, 20), JiraTheme.TextSecondary, TextFormatFlags.Left);
        }
    }

    private sealed class LogTimeDialog : Form
    {
        private readonly NumericUpDown _hours = new() { Minimum = 1, Maximum = 100, Value = 1, Width = 120 };
        private readonly TextBox _comment = JiraControlFactory.CreateTextBox();
        public LogTimeDialog()
        {
            Text = "Log Time";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(380, 220);
            MinimumSize = new Size(380, 220);
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = JiraTheme.BgSurface;
            _comment.Multiline = true;
            _comment.Height = 80;
            _comment.Dock = DockStyle.Fill;
            var save = JiraControlFactory.CreatePrimaryButton("Save");
            var cancel = JiraControlFactory.CreateSecondaryButton("Cancel");
            save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = JiraTheme.BgSurface };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(16), BackColor = JiraTheme.BgSurface };
            layout.Controls.Add(JiraControlFactory.CreateLabel("Hours", true), 0, 0);
            layout.Controls.Add(_hours, 0, 1);
            layout.Controls.Add(JiraControlFactory.CreateLabel("Comment", true), 0, 2);
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
            Text = "Edit Labels";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
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

            var save = JiraControlFactory.CreatePrimaryButton("Apply");
            var cancel = JiraControlFactory.CreateSecondaryButton("Cancel");
            save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

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

            var caption = JiraControlFactory.CreateLabel("Choose one or more labels", true);
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
            Text = "Assign Users";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
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

            var save = JiraControlFactory.CreatePrimaryButton("Apply");
            var cancel = JiraControlFactory.CreateSecondaryButton("Cancel");
            save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

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

            var caption = JiraControlFactory.CreateLabel("Choose one or more assignees", true);
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








































