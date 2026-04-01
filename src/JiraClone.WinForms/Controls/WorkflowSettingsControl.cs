using System.Drawing.Drawing2D;
using JiraClone.Application.Models;
using JiraClone.Application.Roles;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Helpers;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public class WorkflowSettingsControl : UserControl
{
    private readonly AppSession _session;
    private readonly ListView _statuses = CreateListView();
    private readonly ListView _transitions = CreateListView();
    private readonly Panel _canvas = new() { Dock = DockStyle.Top, Height = 280, BackColor = JiraTheme.BgSurface, Padding = new Padding(18, 14, 18, 18) };
    private readonly Label _statusesEmptyState = CreateEmptyState("No workflow statuses configured.");
    private readonly Label _transitionsEmptyState = CreateEmptyState("No workflow transitions configured.");
    private readonly Button _addStatus = JiraControlFactory.CreateSecondaryButton("Add Status");
    private readonly Button _editStatus = JiraControlFactory.CreateSecondaryButton("Edit Status");
    private readonly Button _deleteStatus = JiraControlFactory.CreateSecondaryButton("Delete Status");
    private readonly Button _addTransition = JiraControlFactory.CreateSecondaryButton("Add Transition");
    private readonly Button _editTransition = JiraControlFactory.CreateSecondaryButton("Edit Transition");
    private readonly Button _deleteTransition = JiraControlFactory.CreateSecondaryButton("Delete Transition");
    private readonly Label _summary = JiraControlFactory.CreateLabel("Shape the project workflow, control who can move issues, and keep the board aligned with real delivery stages.", true);
    private WorkflowDefinitionDto? _workflow;
    private IReadOnlyList<Role> _availableRoles = Array.Empty<Role>();
    private IReadOnlyList<WorkflowStatusOptionDto> _visibleStatuses = Array.Empty<WorkflowStatusOptionDto>();
    private IReadOnlyList<WorkflowTransitionDto> _visibleTransitions = Array.Empty<WorkflowTransitionDto>();
    private bool _isLoading;
    private string _shellSearch = string.Empty;

    public WorkflowSettingsControl(AppSession session)
    {
        _session = session;
        Dock = DockStyle.Fill;
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;

        ConfigureButtons();
        ConfigureLists();
        WireEvents();

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 60,
            Padding = new Padding(12, 8, 12, 10),
            BackColor = JiraTheme.BgSurface,
            WrapContents = false,
            AutoScroll = true
        };
        actions.Controls.AddRange([_addStatus, _editStatus, _deleteStatus, _addTransition, _editTransition, _deleteTransition]);

        _summary.Dock = DockStyle.Top;
        _summary.Height = 44;
        _summary.Padding = new Padding(14, 10, 14, 0);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.Border,
            SplitterWidth = 6,
            FixedPanel = FixedPanel.None
        };
        split.HandleCreated += (_, _) => SplitContainerHelper.ConfigureSafeLayout(split, 350, 300, 360);
        split.SizeChanged += (_, _) => SplitContainerHelper.ConfigureSafeLayout(split, 350, 300, 360);
        split.Panel1.Controls.Add(BuildListPanel("Statuses", "Board columns will mirror these statuses automatically.", _statuses, _statusesEmptyState));
        split.Panel2.Controls.Add(BuildListPanel("Transitions", "Choose where issues can move next and which roles can do it.", _transitions, _transitionsEmptyState));

        Controls.Add(split);
        Controls.Add(_canvas);
        Controls.Add(_summary);
        Controls.Add(actions);
    }

    public Task RefreshAsync() => LoadDataAsync();

    public void SetShellSearch(string value)
    {
        _shellSearch = value?.Trim() ?? string.Empty;
        BindWorkflow();
    }

    private WorkflowStatusOptionDto? SelectedStatus => _statuses.SelectedItems.Count == 0 ? null : _statuses.SelectedItems[0].Tag as WorkflowStatusOptionDto;
    private WorkflowTransitionDto? SelectedTransition => _transitions.SelectedItems.Count == 0 ? null : _transitions.SelectedItems[0].Tag as WorkflowTransitionDto;

    private void ConfigureButtons()
    {
        foreach (var button in new[] { _addStatus, _editStatus, _deleteStatus, _addTransition, _editTransition, _deleteTransition })
        {
            button.AutoSize = false;
            button.Height = 40;
        }

        _addStatus.Width = 110;
        _editStatus.Width = 110;
        _deleteStatus.Width = 118;
        _addTransition.Width = 132;
        _editTransition.Width = 132;
        _deleteTransition.Width = 140;
    }

    private void ConfigureLists()
    {
        JiraTheme.StyleListView(_statuses);
        JiraTheme.StyleListView(_transitions);

        _statuses.Columns.Add("Status", 180);
        _statuses.Columns.Add("Category", 120);
        _statuses.Columns.Add("Color", 110);

        _transitions.Columns.Add("Transition", 190);
        _transitions.Columns.Add("From", 120);
        _transitions.Columns.Add("To", 120);
        _transitions.Columns.Add("Roles", 220);

        _statuses.SelectedIndexChanged += (_, _) =>
        {
            UpdateActionState();
            _canvas.Invalidate();
        };
        _transitions.SelectedIndexChanged += (_, _) =>
        {
            UpdateActionState();
            _canvas.Invalidate();
        };
        _statuses.DoubleClick += async (_, _) => await EditStatusAsync();
        _transitions.DoubleClick += async (_, _) => await EditTransitionAsync();
        _statuses.Resize += (_, _) => ApplyResponsiveColumns();
        _transitions.Resize += (_, _) => ApplyResponsiveColumns();
        _canvas.Resize += (_, _) => _canvas.Invalidate();
        _canvas.Paint += PaintCanvas;
    }

    private void WireEvents()
    {
        _addStatus.Click += async (_, _) => await AddStatusAsync();
        _editStatus.Click += async (_, _) => await EditStatusAsync();
        _deleteStatus.Click += async (_, _) => await DeleteStatusAsync();
        _addTransition.Click += async (_, _) => await AddTransitionAsync();
        _editTransition.Click += async (_, _) => await EditTransitionAsync();
        _deleteTransition.Click += async (_, _) => await DeleteTransitionAsync();
        VisibleChanged += async (_, _) =>
        {
            if (Visible)
            {
                await LoadDataAsync();
            }
        };
        Load += async (_, _) => await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        if (_isLoading || DesignMode)
        {
            return;
        }

        try
        {
            _isLoading = true;
            var project = await _session.RunSerializedAsync(() => _session.Projects.GetActiveProjectAsync());
            if (project is null)
            {
                _workflow = null;
                _availableRoles = Array.Empty<Role>();
                BindWorkflow();
                return;
            }

            _availableRoles = await _session.UserCommands.GetRolesAsync();
            _workflow = await _session.Workflows.GetDefaultWorkflowAsync(project.Id);
            BindWorkflow();
        }
        catch (Exception ex)
        {
            ErrorDialogService.Show(ex);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void BindWorkflow()
    {
        var statuses = (_workflow?.Statuses ?? Array.Empty<WorkflowStatusOptionDto>())
            .Where(status => string.IsNullOrWhiteSpace(_shellSearch)
                || status.Name.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)
                || status.Category.ToString().Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)
                || status.Color.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase))
            .OrderBy(status => status.DisplayOrder)
            .ToList();

        var visibleStatusIds = statuses.Select(x => x.Id).ToHashSet();
        var transitions = (_workflow?.Transitions ?? Array.Empty<WorkflowTransitionDto>())
            .Where(transition => visibleStatusIds.Contains(transition.FromStatusId) || visibleStatusIds.Contains(transition.ToStatusId) || string.IsNullOrWhiteSpace(_shellSearch))
            .Where(transition => string.IsNullOrWhiteSpace(_shellSearch)
                || transition.Name.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)
                || transition.FromStatusName.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)
                || transition.ToStatusName.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)
                || transition.AllowedRoleNames.Any(role => role.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(transition => transition.FromStatusName)
            .ThenBy(transition => transition.ToStatusName)
            .ThenBy(transition => transition.Name)
            .ToList();

        _visibleStatuses = statuses;
        _visibleTransitions = transitions;

        BindStatuses(statuses);
        BindTransitions(transitions);
        ApplyResponsiveColumns();

        _statusesEmptyState.Visible = statuses.Count == 0;
        _statuses.Visible = statuses.Count > 0;
        _transitionsEmptyState.Visible = transitions.Count == 0;
        _transitions.Visible = transitions.Count > 0;
        UpdateActionState();
        _canvas.Invalidate();
    }

    private void BindStatuses(IEnumerable<WorkflowStatusOptionDto> statuses)
    {
        var selectedId = SelectedStatus?.Id;
        _statuses.BeginUpdate();
        _statuses.Items.Clear();
        foreach (var status in statuses)
        {
            var item = new ListViewItem(IssueDisplayText.TranslateStatus(status.Name)) { Tag = status };
            item.SubItems.Add(status.Category.ToString());
            item.SubItems.Add(status.Color);
            _statuses.Items.Add(item);
            if (selectedId.HasValue && selectedId.Value == status.Id)
            {
                item.Selected = true;
            }
        }
        _statuses.EndUpdate();
    }

    private void BindTransitions(IEnumerable<WorkflowTransitionDto> transitions)
    {
        var selectedId = SelectedTransition?.Id;
        _transitions.BeginUpdate();
        _transitions.Items.Clear();
        foreach (var transition in transitions)
        {
            var item = new ListViewItem(transition.Name) { Tag = transition };
            item.SubItems.Add(IssueDisplayText.TranslateStatus(transition.FromStatusName));
            item.SubItems.Add(IssueDisplayText.TranslateStatus(transition.ToStatusName));
            item.SubItems.Add(string.Join(", ", transition.AllowedRoleNames));
            _transitions.Items.Add(item);
            if (selectedId.HasValue && selectedId.Value == transition.Id)
            {
                item.Selected = true;
            }
        }
        _transitions.EndUpdate();
    }

    private void ApplyResponsiveColumns()
    {
        if (_statuses.ClientSize.Width > 0)
        {
            var nameWidth = 180;
            var categoryWidth = 120;
            _statuses.Columns[0].Width = nameWidth;
            _statuses.Columns[1].Width = categoryWidth;
            _statuses.Columns[2].Width = Math.Max(90, _statuses.ClientSize.Width - nameWidth - categoryWidth - 12);
        }

        if (_transitions.ClientSize.Width > 0)
        {
            var nameWidth = 180;
            var fromWidth = 120;
            var toWidth = 120;
            _transitions.Columns[0].Width = nameWidth;
            _transitions.Columns[1].Width = fromWidth;
            _transitions.Columns[2].Width = toWidth;
            _transitions.Columns[3].Width = Math.Max(180, _transitions.ClientSize.Width - nameWidth - fromWidth - toWidth - 12);
        }
    }

    private async Task AddStatusAsync()
    {
        var project = await _session.RunSerializedAsync(() => _session.Projects.GetActiveProjectAsync());
        if (project is null)
        {
            return;
        }

        using var dialog = new WorkflowStatusDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            await _session.Workflows.CreateStatusAsync(project.Id, dialog.StatusName, dialog.SelectedColorHex, dialog.StatusCategory);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ErrorDialogService.Show(ex);
        }
    }

    private async Task EditStatusAsync()
    {
        if (SelectedStatus is not { } status)
        {
            return;
        }

        using var dialog = new WorkflowStatusDialog(status.Name, status.Color, status.Category);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            await _session.Workflows.UpdateStatusAsync(status.Id, dialog.StatusName, dialog.SelectedColorHex, dialog.StatusCategory);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ErrorDialogService.Show(ex);
        }
    }

    private async Task DeleteStatusAsync()
    {
        if (SelectedStatus is not { } status)
        {
            return;
        }

        if (MessageBox.Show(this, $"Delete status '{status.Name}'? Related board column and transitions will be removed too.", "Delete Workflow Status", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            await _session.Workflows.DeleteStatusAsync(status.Id);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ErrorDialogService.Show(ex);
        }
    }

    private async Task AddTransitionAsync()
    {
        var project = await _session.RunSerializedAsync(() => _session.Projects.GetActiveProjectAsync());
        if (project is null || _workflow is null)
        {
            return;
        }

        using var dialog = new WorkflowTransitionDialog(_workflow.Statuses, _availableRoles);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            await _session.Workflows.CreateTransitionAsync(project.Id, dialog.FromStatusId, dialog.ToStatusId, dialog.TransitionName, dialog.SelectedRoleNames);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ErrorDialogService.Show(ex);
        }
    }

    private async Task EditTransitionAsync()
    {
        if (_workflow is null || SelectedTransition is not { } transition)
        {
            return;
        }

        using var dialog = new WorkflowTransitionDialog(_workflow.Statuses, _availableRoles, transition);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            await _session.Workflows.UpdateTransitionAsync(transition.Id, dialog.TransitionName, dialog.SelectedRoleNames);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ErrorDialogService.Show(ex);
        }
    }

    private async Task DeleteTransitionAsync()
    {
        if (SelectedTransition is not { } transition)
        {
            return;
        }

        if (MessageBox.Show(this, $"Delete transition '{transition.Name}'?", "Delete Workflow Transition", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            await _session.Workflows.DeleteTransitionAsync(transition.Id);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ErrorDialogService.Show(ex);
        }
    }

    private void UpdateActionState()
    {
        var canManage = _session.Authorization.IsInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var hasWorkflow = _workflow is not null;
        _addStatus.Enabled = canManage && hasWorkflow;
        _editStatus.Enabled = canManage && SelectedStatus is not null;
        _deleteStatus.Enabled = canManage && SelectedStatus is not null;
        _addTransition.Enabled = canManage && hasWorkflow && _workflow?.Statuses.Count > 1;
        _editTransition.Enabled = canManage && SelectedTransition is not null;
        _deleteTransition.Enabled = canManage && SelectedTransition is not null;
    }

    private void PaintCanvas(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(JiraTheme.BgSurface);

        var bounds = new Rectangle(16, 16, Math.Max(0, _canvas.ClientSize.Width - 32), Math.Max(0, _canvas.ClientSize.Height - 32));
        if (_visibleStatuses.Count == 0)
        {
            TextRenderer.DrawText(e.Graphics, "Workflow preview will appear here once statuses are available.", JiraTheme.FontBody, bounds, JiraTheme.TextSecondary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var statuses = _visibleStatuses.OrderBy(x => x.DisplayOrder).ToList();
        var selectedStatusId = SelectedStatus?.Id;
        var selectedTransitionId = SelectedTransition?.Id;
        var nodeWidth = statuses.Count >= 5 ? 132 : 152;
        var nodeHeight = 66;
        var spacing = statuses.Count == 1 ? 0 : Math.Max(18, (bounds.Width - (nodeWidth * statuses.Count)) / Math.Max(1, statuses.Count - 1));
        if ((nodeWidth * statuses.Count) + (spacing * Math.Max(0, statuses.Count - 1)) > bounds.Width)
        {
            spacing = 18;
            nodeWidth = Math.Max(112, (bounds.Width - (spacing * Math.Max(0, statuses.Count - 1))) / statuses.Count);
        }

        var top = bounds.Top + 98;
        var left = bounds.Left + Math.Max(0, (bounds.Width - ((nodeWidth * statuses.Count) + (spacing * Math.Max(0, statuses.Count - 1)))) / 2);
        var layout = new Dictionary<int, Rectangle>();

        for (var index = 0; index < statuses.Count; index++)
        {
            var rect = new Rectangle(left + (index * (nodeWidth + spacing)), top, nodeWidth, nodeHeight);
            layout[statuses[index].Id] = rect;
        }

        using var arrowCap = new AdjustableArrowCap(4, 6);
        using var regularPen = new Pen(JiraTheme.Border, 2f) { CustomEndCap = arrowCap };
        using var selectedPen = new Pen(JiraTheme.Primary, 3f) { CustomEndCap = arrowCap };
        using var softPen = new Pen(Color.FromArgb(160, JiraTheme.Border), 1.5f) { DashStyle = DashStyle.Dash, CustomEndCap = arrowCap };

        foreach (var transition in _visibleTransitions)
        {
            if (!layout.TryGetValue(transition.FromStatusId, out var fromRect) || !layout.TryGetValue(transition.ToStatusId, out var toRect))
            {
                continue;
            }

            var isSelected = selectedTransitionId == transition.Id;
            var pen = isSelected ? selectedPen : regularPen;
            if (transition.FromStatusId == transition.ToStatusId)
            {
                continue;
            }

            var forward = fromRect.Left <= toRect.Left;
            if (forward)
            {
                var start = new Point(fromRect.Right, fromRect.Top + (fromRect.Height / 2));
                var end = new Point(toRect.Left, toRect.Top + (toRect.Height / 2));
                var c1 = new Point(start.X + 28, start.Y - 36);
                var c2 = new Point(end.X - 28, end.Y - 36);
                e.Graphics.DrawBezier(pen, start, c1, c2, end);
            }
            else
            {
                var start = new Point(fromRect.Left + (fromRect.Width / 2), fromRect.Top);
                var end = new Point(toRect.Left + (toRect.Width / 2), toRect.Top);
                var arcHeight = Math.Max(34, 34 + (Math.Abs(fromRect.Left - toRect.Left) / 4));
                var c1 = new Point(start.X, start.Y - arcHeight);
                var c2 = new Point(end.X, end.Y - arcHeight);
                e.Graphics.DrawBezier(isSelected ? selectedPen : softPen, start, c1, c2, end);
            }
        }

        foreach (var status in statuses)
        {
            var rect = layout[status.Id];
            var fill = ParseStatusColor(status.Color, status.Category);
            var stroke = selectedStatusId == status.Id ? JiraTheme.Primary : Color.FromArgb(120, fill);
            using var path = GraphicsHelper.CreateRoundedPath(rect, 14);
            using var fillBrush = new SolidBrush(fill);
            using var borderPen = new Pen(stroke, selectedStatusId == status.Id ? 3f : 1.5f);
            e.Graphics.FillPath(fillBrush, path);
            e.Graphics.DrawPath(borderPen, path);

            var titleRect = new Rectangle(rect.X + 12, rect.Y + 10, rect.Width - 24, 24);
            var categoryRect = new Rectangle(rect.X + 12, rect.Y + 34, rect.Width - 24, 18);
            var textColor = GetTextColor(status.Category);
            TextRenderer.DrawText(e.Graphics, IssueDisplayText.TranslateStatus(status.Name), JiraTheme.FontColumnHeader, titleRect, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, status.Category.ToString(), JiraTheme.FontCaption, categoryRect, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        var captionRect = new Rectangle(bounds.Left, bounds.Top, bounds.Width, 30);
        TextRenderer.DrawText(e.Graphics, _workflow is null ? "Default workflow" : $"{_workflow.Name} workflow map", JiraTheme.FontColumnHeader, captionRect, JiraTheme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(e.Graphics, "Solid arrows show available transitions. Select a status or transition below to highlight it here.", JiraTheme.FontCaption, new Rectangle(bounds.Left, bounds.Top + 28, bounds.Width, 24), JiraTheme.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }

    private static Control BuildListPanel(string title, string subtitle, Control list, Control emptyState)
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
        var subtitleLabel = JiraControlFactory.CreateLabel(subtitle, true);
        subtitleLabel.Dock = DockStyle.Top;
        subtitleLabel.Height = 28;
        subtitleLabel.Padding = new Padding(14, 0, 14, 0);

        var titleLabel = JiraControlFactory.CreateLabel(title);
        titleLabel.Font = JiraTheme.FontColumnHeader;
        titleLabel.Dock = DockStyle.Top;
        titleLabel.Height = 34;
        titleLabel.Padding = new Padding(14, 10, 14, 0);

        host.Controls.Add(emptyState);
        host.Controls.Add(list);
        host.Controls.Add(subtitleLabel);
        host.Controls.Add(titleLabel);
        return host;
    }

    private static ListView CreateListView() => new()
    {
        Dock = DockStyle.Fill,
        FullRowSelect = true,
        MultiSelect = false,
        View = View.Details,
        BorderStyle = BorderStyle.None,
        BackColor = JiraTheme.BgSurface,
        ForeColor = JiraTheme.TextPrimary,
        Font = JiraTheme.FontBody,
        HideSelection = false
    };

    private static Label CreateEmptyState(string text)
    {
        var label = JiraControlFactory.CreateLabel(text, true);
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleCenter;
        label.Visible = false;
        return label;
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

    private static Color GetTextColor(StatusCategory category) => category == StatusCategory.ToDo ? JiraTheme.TextPrimary : Color.White;

    private sealed class WorkflowStatusDialog : Form
    {
        private readonly TextBox _name = JiraControlFactory.CreateTextBox();
        private readonly ComboBox _category = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody, Width = 280 };
        private readonly Panel _colorPreview = new() { Width = 44, Height = 28, BackColor = JiraTheme.Primary };
        private readonly Button _pickColor = JiraControlFactory.CreateSecondaryButton("Pick Color");
        private string _selectedColorHex;

        public WorkflowStatusDialog(string? name = null, string? colorHex = null, StatusCategory category = StatusCategory.ToDo)
        {
            Text = string.IsNullOrWhiteSpace(name) ? "Add Workflow Status" : "Edit Workflow Status";
            AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
            Width = 390;
            Height = 280;
            MinimumSize = new Size(390, 280);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = JiraTheme.BgSurface;
            Font = JiraTheme.FontBody;

            _selectedColorHex = string.IsNullOrWhiteSpace(colorHex) ? "#42526E" : colorHex;
            _name.Text = name ?? string.Empty;
            _category.DataSource = Enum.GetValues<StatusCategory>();
            _category.SelectedItem = category;
            _colorPreview.BackColor = ParseStatusColor(_selectedColorHex, category);
            _pickColor.Click += (_, _) => PickColor();

            var save = JiraControlFactory.CreatePrimaryButton("Save");
            var cancel = JiraControlFactory.CreateSecondaryButton("Cancel");
            save.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(_name.Text))
                {
                    ErrorDialogService.Show("Status name is required.");
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            };
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            var colorRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false, BackColor = JiraTheme.BgSurface, Margin = new Padding(0) };
            colorRow.Controls.Add(_colorPreview);
            colorRow.Controls.Add(_pickColor);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = JiraTheme.BgSurface };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);

            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16), BackColor = JiraTheme.BgSurface };
            layout.Controls.Add(JiraControlFactory.CreateLabel("Name", true));
            layout.Controls.Add(_name);
            layout.Controls.Add(JiraControlFactory.CreateLabel("Category", true));
            layout.Controls.Add(_category);
            layout.Controls.Add(JiraControlFactory.CreateLabel("Color", true));
            layout.Controls.Add(colorRow);
            Controls.Add(layout);
            Controls.Add(buttons);
        }

        public string StatusName => _name.Text.Trim();
        public string SelectedColorHex => _selectedColorHex;
        public StatusCategory StatusCategory => _category.SelectedItem is StatusCategory category ? category : StatusCategory.ToDo;

        private void PickColor()
        {
            using var dialog = new ColorDialog { FullOpen = true, Color = ParseStatusColor(_selectedColorHex, StatusCategory) };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            _selectedColorHex = ColorTranslator.ToHtml(dialog.Color);
            _colorPreview.BackColor = dialog.Color;
        }
    }

    private sealed class WorkflowTransitionDialog : Form
    {
        private readonly ComboBox _from = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody, Width = 280 };
        private readonly ComboBox _to = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody, Width = 280 };
        private readonly TextBox _name = JiraControlFactory.CreateTextBox();
        private readonly CheckedListBox _roles = new() { CheckOnClick = true, Width = 280, Height = 150, BorderStyle = BorderStyle.FixedSingle, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody };

        public WorkflowTransitionDialog(IReadOnlyList<WorkflowStatusOptionDto> statuses, IReadOnlyList<Role> roles, WorkflowTransitionDto? transition = null)
        {
            Text = transition is null ? "Add Workflow Transition" : "Edit Workflow Transition";
            AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
            Width = 430;
            Height = 470;
            MinimumSize = new Size(430, 470);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = JiraTheme.BgSurface;
            Font = JiraTheme.FontBody;

            _from.DisplayMember = nameof(WorkflowStatusOptionDto.Name);
            _from.ValueMember = nameof(WorkflowStatusOptionDto.Id);
            _to.DisplayMember = nameof(WorkflowStatusOptionDto.Name);
            _to.ValueMember = nameof(WorkflowStatusOptionDto.Id);
            _from.DataSource = statuses.OrderBy(x => x.DisplayOrder).ToList();
            _to.DataSource = statuses.OrderBy(x => x.DisplayOrder).ToList();
            _name.Text = transition?.Name ?? string.Empty;

            foreach (var role in roles.OrderBy(x => x.Name))
            {
                var isChecked = transition?.AllowedRoleNames.Contains(role.Name, StringComparer.OrdinalIgnoreCase)
                    ?? role.Name is RoleCatalog.Admin or RoleCatalog.ProjectManager or RoleCatalog.Developer;
                _roles.Items.Add(role.Name, isChecked);
            }

            if (transition is not null)
            {
                _from.SelectedValue = transition.FromStatusId;
                _to.SelectedValue = transition.ToStatusId;
                _from.Enabled = false;
                _to.Enabled = false;
            }

            var save = JiraControlFactory.CreatePrimaryButton("Save");
            var cancel = JiraControlFactory.CreateSecondaryButton("Cancel");
            save.Click += (_, _) =>
            {
                if (FromStatusId == ToStatusId)
                {
                    ErrorDialogService.Show("Choose two different statuses for the transition.");
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            };
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = JiraTheme.BgSurface };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);

            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16), BackColor = JiraTheme.BgSurface, AutoScroll = true };
            layout.Controls.Add(JiraControlFactory.CreateLabel("From status", true));
            layout.Controls.Add(_from);
            layout.Controls.Add(JiraControlFactory.CreateLabel("To status", true));
            layout.Controls.Add(_to);
            layout.Controls.Add(JiraControlFactory.CreateLabel("Transition name", true));
            layout.Controls.Add(_name);
            layout.Controls.Add(JiraControlFactory.CreateLabel("Allowed roles", true));
            layout.Controls.Add(_roles);
            Controls.Add(layout);
            Controls.Add(buttons);
        }

        public int FromStatusId => _from.SelectedValue is int value ? value : (_from.SelectedItem as WorkflowStatusOptionDto)?.Id ?? 0;
        public int ToStatusId => _to.SelectedValue is int value ? value : (_to.SelectedItem as WorkflowStatusOptionDto)?.Id ?? 0;
        public string TransitionName => string.IsNullOrWhiteSpace(_name.Text) ? $"{(_from.SelectedItem as WorkflowStatusOptionDto)?.Name} to {(_to.SelectedItem as WorkflowStatusOptionDto)?.Name}" : _name.Text.Trim();
        public IReadOnlyList<string> SelectedRoleNames => _roles.CheckedItems.Cast<string>().ToList();
    }
}










