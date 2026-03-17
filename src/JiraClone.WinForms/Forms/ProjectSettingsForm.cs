using JiraClone.Application.Roles;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;
using JiraComponentEntity = JiraClone.Domain.Entities.Component;
using JiraLabelEntity = JiraClone.Domain.Entities.Label;
using JiraProjectVersionEntity = JiraClone.Domain.Entities.ProjectVersion;

namespace JiraClone.WinForms.Forms;

public class ProjectSettingsForm : UserControl
{
    private readonly AppSession _session;
    private readonly TextBox _name = JiraControlFactory.CreateTextBox();
    private readonly TextBox _description = JiraControlFactory.CreateTextBox();
    private readonly ComboBox _category = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody };
    private readonly TextBox _url = JiraControlFactory.CreateTextBox();
    private readonly ListView _members = CreateListView();
    private readonly ListView _columns = CreateListView();
    private readonly ListView _labels = CreateListView();
    private readonly ListView _components = CreateListView();
    private readonly ListView _versions = CreateListView();
    private readonly Button _saveProject = JiraControlFactory.CreatePrimaryButton("Save Project");
    private readonly Button _archiveProject = JiraControlFactory.CreateSecondaryButton("Archive Project");
    private readonly Button _addMember = JiraControlFactory.CreateSecondaryButton("Add Member");
    private readonly Button _changeMemberRole = JiraControlFactory.CreateSecondaryButton("Change Role");
    private readonly Button _removeMember = JiraControlFactory.CreateSecondaryButton("Remove Member");
    private readonly Button _editColumn = JiraControlFactory.CreateSecondaryButton("Edit Column");
    private readonly Button _addLabel = JiraControlFactory.CreateSecondaryButton("Add Label");
    private readonly Button _editLabel = JiraControlFactory.CreateSecondaryButton("Edit Label");
    private readonly Button _deleteLabel = JiraControlFactory.CreateSecondaryButton("Delete Label");
    private readonly Button _addComponent = JiraControlFactory.CreateSecondaryButton("Add Component");
    private readonly Button _editComponent = JiraControlFactory.CreateSecondaryButton("Edit Component");
    private readonly Button _deleteComponent = JiraControlFactory.CreateSecondaryButton("Delete Component");
    private readonly Button _addVersion = JiraControlFactory.CreateSecondaryButton("Add Version");
    private readonly Button _editVersion = JiraControlFactory.CreateSecondaryButton("Edit Version");
    private readonly Button _deleteVersion = JiraControlFactory.CreateSecondaryButton("Delete Version");
    private readonly Button _markVersionReleased = JiraControlFactory.CreateSecondaryButton("Mark Released");
    private readonly Label _memberCountBadge = CreateBadgeLabel();
    private readonly Label _columnCountBadge = CreateBadgeLabel();
    private readonly Label _labelCountBadge = CreateBadgeLabel();
    private readonly Label _componentCountBadge = CreateBadgeLabel();
    private readonly Label _versionCountBadge = CreateBadgeLabel();
    private readonly Label _membersEmptyState = CreateEmptyState("No members on this project yet.");
    private readonly Label _columnsEmptyState = CreateEmptyState("No board columns configured.");
    private readonly Label _labelsEmptyState = CreateEmptyState("No labels created for this project yet.");
    private readonly Label _componentsEmptyState = CreateEmptyState("No components created for this project yet.");
    private readonly Label _versionsEmptyState = CreateEmptyState("No versions created for this project yet.");
    private Project? _project;
    private bool _isLoading;
    private string _shellSearch = string.Empty;

    public ProjectSettingsForm(AppSession session)
    {
        _session = session;
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;

        _name.MinimumSize = new Size(360, 38);
        _url.MinimumSize = new Size(360, 38);
        _category.MinimumSize = new Size(360, 38);
        _description.Multiline = true;
        _description.Height = 96;
        _description.ScrollBars = ScrollBars.Vertical;
        _category.DataSource = Enum.GetValues<ProjectCategory>();

        ConfigureListViews();
        WireActions();
        ConfigureActionButtons();

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = JiraTheme.FontBody };
        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildMembersTab());
        tabs.TabPages.Add(BuildColumnsTab());
        tabs.TabPages.Add(BuildLabelsTab());
        tabs.TabPages.Add(BuildComponentsTab());
        tabs.TabPages.Add(BuildVersionsTab());

        Controls.Add(tabs);
        Controls.Add(BuildHeader());
        Load += async (_, _) => await LoadProjectAsync();
        UpdateActionState();
    }

    private ProjectMember? SelectedMember => _members.SelectedItems.Count == 0 ? null : _members.SelectedItems[0].Tag as ProjectMember;
    private BoardColumn? SelectedColumn => _columns.SelectedItems.Count == 0 ? null : _columns.SelectedItems[0].Tag as BoardColumn;
    private JiraLabelEntity? SelectedProjectLabel => _labels.SelectedItems.Count == 0 ? null : _labels.SelectedItems[0].Tag as JiraLabelEntity;
    private JiraComponentEntity? SelectedProjectComponent => _components.SelectedItems.Count == 0 ? null : _components.SelectedItems[0].Tag as JiraComponentEntity;
    private JiraProjectVersionEntity? SelectedProjectVersion => _versions.SelectedItems.Count == 0 ? null : _versions.SelectedItems[0].Tag as JiraProjectVersionEntity;

    public Task RefreshProjectAsync() => LoadProjectAsync();

    public void SetShellSearch(string value)
    {
        _shellSearch = value?.Trim() ?? string.Empty;
        BindProjectLists();
    }

    private void ConfigureListViews()
    {
        JiraTheme.StyleListView(_members);
        JiraTheme.StyleListView(_columns);
        JiraTheme.StyleListView(_labels);
        JiraTheme.StyleListView(_components);
        JiraTheme.StyleListView(_versions);

        _members.Columns.Add("Member", 220);
        _members.Columns.Add("Project Role", 140);
        _members.Columns.Add("System Roles", 280);

        _columns.Columns.Add("Column", 220);
        _columns.Columns.Add("Status", 140);
        _columns.Columns.Add("WIP", 100);

        _labels.Columns.Add("Label", 220);
        _labels.Columns.Add("Color", 140);

        _components.Columns.Add("Component", 200);
        _components.Columns.Add("Lead", 140);
        _components.Columns.Add("Description", 280);

        _versions.Columns.Add("Version", 220);
        _versions.Columns.Add("Release Date", 140);
        _versions.Columns.Add("Status", 120);

        _members.SelectedIndexChanged += (_, _) => UpdateActionState();
        _columns.SelectedIndexChanged += (_, _) => UpdateActionState();
        _labels.SelectedIndexChanged += (_, _) => UpdateActionState();
        _components.SelectedIndexChanged += (_, _) => UpdateActionState();
        _versions.SelectedIndexChanged += (_, _) => UpdateActionState();

        _members.Resize += (_, _) => ApplyResponsiveColumns();
        _columns.Resize += (_, _) => ApplyResponsiveColumns();
        _labels.Resize += (_, _) => ApplyResponsiveColumns();
        _components.Resize += (_, _) => ApplyResponsiveColumns();
        _versions.Resize += (_, _) => ApplyResponsiveColumns();
    }

    private void WireActions()
    {
        _saveProject.Click += async (_, _) => await SaveProjectAsync();
        _addMember.Click += async (_, _) => await AddMemberAsync();
        _changeMemberRole.Click += async (_, _) => await ChangeMemberRoleAsync();
        _removeMember.Click += async (_, _) => await RemoveMemberAsync();
        _editColumn.Click += async (_, _) => await EditColumnAsync();
        _addLabel.Click += async (_, _) => await AddLabelAsync();
        _editLabel.Click += async (_, _) => await EditLabelAsync();
        _deleteLabel.Click += async (_, _) => await DeleteLabelAsync();
        _addComponent.Click += async (_, _) => await AddComponentAsync();
        _editComponent.Click += async (_, _) => await EditComponentAsync();
        _deleteComponent.Click += async (_, _) => await DeleteComponentAsync();
        _addVersion.Click += async (_, _) => await AddVersionAsync();
        _editVersion.Click += async (_, _) => await EditVersionAsync();
        _deleteVersion.Click += async (_, _) => await DeleteVersionAsync();
        _markVersionReleased.Click += async (_, _) => await MarkVersionReleasedAsync();
        _archiveProject.Click += async (_, _) => await ArchiveProjectAsync();
    }

    private void ConfigureActionButtons()
    {
        ConfigureActionButton(_saveProject, 118);
        ConfigureActionButton(_archiveProject, 138);
        StyleDangerButton(_archiveProject);
        ConfigureActionButton(_addMember, 116);
        ConfigureActionButton(_changeMemberRole, 116);
        ConfigureActionButton(_removeMember, 126);
        ConfigureActionButton(_editColumn, 104);
        ConfigureActionButton(_addLabel, 108);
        ConfigureActionButton(_editLabel, 108);
        ConfigureActionButton(_deleteLabel, 116);
        ConfigureActionButton(_addComponent, 132);
        ConfigureActionButton(_editComponent, 132);
        ConfigureActionButton(_deleteComponent, 144);
        ConfigureActionButton(_addVersion, 124);
        ConfigureActionButton(_editVersion, 124);
        ConfigureActionButton(_deleteVersion, 132);
        ConfigureActionButton(_markVersionReleased, 132);
    }

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 110, BackColor = JiraTheme.BgPage, Padding = new Padding(20, 18, 20, 8) };
        var title = JiraControlFactory.CreateLabel("Project Settings");
        title.Font = JiraTheme.FontH1;
        title.Location = new Point(0, 0);

        var caption = JiraControlFactory.CreateLabel("Adjust project details, members, board structure, labels, components, and release versions without leaving the desktop flow.", true);
        caption.Location = new Point(0, 42);

        var badges = new FlowLayoutPanel
        {
            Location = new Point(0, 72),
            AutoSize = true,
            WrapContents = true,
            BackColor = JiraTheme.BgPage,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        badges.Controls.AddRange([_memberCountBadge, _columnCountBadge, _labelCountBadge, _componentCountBadge, _versionCountBadge]);

        header.Controls.Add(title);
        header.Controls.Add(caption);
        header.Controls.Add(badges);
        return header;
    }

    private TabPage BuildGeneralTab()
    {
        var page = CreatePage("General");
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, Padding = new Padding(28, 24, 28, 24), BackColor = JiraTheme.BgSurface, AutoSize = true, AutoScroll = false, RowCount = 5 };
        layout.MaximumSize = new Size(860, 0);
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(layout, 0, "Name", _name);
        AddRow(layout, 1, "Description", _description);
        AddRow(layout, 2, "Category", _category);
        AddRow(layout, 3, "URL", _url);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, BackColor = JiraTheme.BgSurface, Margin = new Padding(0) };
        actions.Controls.Add(_saveProject);
        actions.Controls.Add(_archiveProject);
        layout.Controls.Add(actions, 1, 4);
        page.Controls.Add(WrapSurface(layout));
        return page;
    }

    private TabPage BuildMembersTab()
    {
        var page = CreatePage("Members");
        var actions = CreateActionBar(_addMember, _changeMemberRole, _removeMember);
        page.Controls.Add(WrapSurface(BuildListSurface(_members, _membersEmptyState, actions)));
        return page;
    }

    private TabPage BuildColumnsTab()
    {
        var page = CreatePage("Board Columns");
        var actions = CreateActionBar(_editColumn);
        page.Controls.Add(WrapSurface(BuildListSurface(_columns, _columnsEmptyState, actions)));
        return page;
    }

    private TabPage BuildLabelsTab()
    {
        var page = CreatePage("Labels");
        var actions = CreateActionBar(_addLabel, _editLabel, _deleteLabel);
        page.Controls.Add(WrapSurface(BuildListSurface(_labels, _labelsEmptyState, actions)));
        return page;
    }

    private TabPage BuildComponentsTab()
    {
        var page = CreatePage("Components");
        var actions = CreateActionBar(_addComponent, _editComponent, _deleteComponent);
        page.Controls.Add(WrapSurface(BuildListSurface(_components, _componentsEmptyState, actions)));
        return page;
    }

    private TabPage BuildVersionsTab()
    {
        var page = CreatePage("Versions");
        var actions = CreateActionBar(_addVersion, _editVersion, _deleteVersion, _markVersionReleased);
        page.Controls.Add(WrapSurface(BuildListSurface(_versions, _versionsEmptyState, actions)));
        return page;
    }

    private static Control BuildListSurface(Control list, Control emptyState, Control actions)
    {
        var surface = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
        surface.Controls.Add(emptyState);
        surface.Controls.Add(list);
        surface.Controls.Add(actions);
        return surface;
    }

    private static FlowLayoutPanel CreateActionBar(params Control[] controls)
    {
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(12, 8, 12, 10), BackColor = JiraTheme.BgSurface };
        actions.Controls.AddRange(controls);
        return actions;
    }

    private static Control WrapSurface(Control inner)
    {
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 20), BackColor = JiraTheme.BgPage };
        var surface = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
        surface.Paint += (_, e) =>
        {
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, surface.Width - 1, surface.Height - 1);
        };
        inner.Dock = DockStyle.Fill;
        surface.Controls.Add(inner);
        host.Controls.Add(surface);
        return host;
    }

    private async Task LoadProjectAsync()
    {
        if (_isLoading || !Visible)
        {
            return;
        }

        try
        {
            _isLoading = true;
            _project = await _session.RunSerializedAsync(() => _session.Projects.GetActiveProjectAsync());
            if (_project is null)
            {
                return;
            }

            _name.Text = _project.Name;
            _description.Text = _project.Description;
            _category.SelectedItem = _project.Category;
            _url.Text = _project.Url;
            _memberCountBadge.Text = _project.Members.Count == 1 ? "1 member" : $"{_project.Members.Count} members";
            _columnCountBadge.Text = _project.BoardColumns.Count == 1 ? "1 column" : $"{_project.BoardColumns.Count} columns";
            _labelCountBadge.Text = _project.Labels.Count == 1 ? "1 label" : $"{_project.Labels.Count} labels";
            _componentCountBadge.Text = _project.Components.Count == 1 ? "1 component" : $"{_project.Components.Count} components";
            _versionCountBadge.Text = _project.Versions.Count == 1 ? "1 version" : $"{_project.Versions.Count} versions";
            BindProjectLists();
            UpdateActionState();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void BindProjectLists()
    {
        if (_project is null)
        {
            return;
        }

        var members = _project.Members
            .Where(x => string.IsNullOrWhiteSpace(_shellSearch)
                || x.User.DisplayName.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)
                || x.ProjectRole.ToString().Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)
                || x.User.UserRoles.Any(r => r.Role.Name.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.User.DisplayName)
            .ToList();

        var columns = _project.BoardColumns
            .Where(x => string.IsNullOrWhiteSpace(_shellSearch)
                || x.Name.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)
                || x.StatusCode.ToString().Contains(_shellSearch, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.DisplayOrder)
            .ToList();

        var labels = _project.Labels
            .Where(x => string.IsNullOrWhiteSpace(_shellSearch)
                || x.Name.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)
                || x.Color.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name)
            .ToList();

        var components = _project.Components
            .Where(x => string.IsNullOrWhiteSpace(_shellSearch)
                || x.Name.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)
                || (x.Description?.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase) ?? false)
                || (x.LeadUser?.DisplayName.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(x => x.Name)
            .ToList();

        var versions = _project.Versions
            .Where(x => string.IsNullOrWhiteSpace(_shellSearch)
                || x.Name.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)
                || (x.Description?.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase) ?? false)
                || (x.IsReleased ? "Released" : "Planned").Contains(_shellSearch, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.ReleaseDate)
            .ThenBy(x => x.Name)
            .ToList();

        BindMembers(members);
        BindColumns(columns);
        BindLabels(labels);
        BindComponents(components);
        BindVersions(versions);
        ApplyResponsiveColumns();

        _membersEmptyState.Visible = members.Count == 0;
        _members.Visible = members.Count > 0;
        _columnsEmptyState.Visible = columns.Count == 0;
        _columns.Visible = columns.Count > 0;
        _labelsEmptyState.Visible = labels.Count == 0;
        _labels.Visible = labels.Count > 0;
        _componentsEmptyState.Visible = components.Count == 0;
        _components.Visible = components.Count > 0;
        _versionsEmptyState.Visible = versions.Count == 0;
        _versions.Visible = versions.Count > 0;
    }

    private void BindMembers(IEnumerable<ProjectMember> members)
    {
        _members.BeginUpdate();
        _members.Items.Clear();
        foreach (var member in members)
        {
            var item = new ListViewItem(member.User.DisplayName) { Tag = member };
            item.SubItems.Add(member.ProjectRole.ToString());
            item.SubItems.Add(string.Join(", ", member.User.UserRoles.Select(x => x.Role.Name)));
            _members.Items.Add(item);
        }
        _members.EndUpdate();
    }

    private void BindColumns(IEnumerable<BoardColumn> columns)
    {
        _columns.BeginUpdate();
        _columns.Items.Clear();
        foreach (var column in columns)
        {
            var item = new ListViewItem(column.Name) { Tag = column };
            item.SubItems.Add(column.StatusCode.ToString());
            item.SubItems.Add(column.WipLimit?.ToString() ?? "-");
            _columns.Items.Add(item);
        }
        _columns.EndUpdate();
    }

    private void BindLabels(IEnumerable<JiraLabelEntity> labels)
    {
        _labels.BeginUpdate();
        _labels.Items.Clear();
        foreach (var label in labels)
        {
            var item = new ListViewItem(label.Name) { Tag = label };
            item.SubItems.Add(label.Color);
            _labels.Items.Add(item);
        }
        _labels.EndUpdate();
    }

    private void BindComponents(IEnumerable<JiraComponentEntity> components)
    {
        _components.BeginUpdate();
        _components.Items.Clear();
        foreach (var component in components)
        {
            var item = new ListViewItem(component.Name) { Tag = component };
            item.SubItems.Add(component.LeadUser?.DisplayName ?? "Unassigned");
            item.SubItems.Add(component.Description ?? "-");
            _components.Items.Add(item);
        }
        _components.EndUpdate();
    }

    private void BindVersions(IEnumerable<JiraProjectVersionEntity> versions)
    {
        _versions.BeginUpdate();
        _versions.Items.Clear();
        foreach (var version in versions)
        {
            var item = new ListViewItem(version.Name) { Tag = version };
            item.SubItems.Add(version.ReleaseDate?.ToString("dd MMM yyyy") ?? "-");
            item.SubItems.Add(version.IsReleased ? "Released" : "Planned");
            _versions.Items.Add(item);
        }
        _versions.EndUpdate();
    }

    private void ApplyResponsiveColumns()
    {
        if (_members.ClientSize.Width > 0)
        {
            var memberWidth = 220;
            var roleWidth = 170;
            var systemRoleWidth = Math.Max(220, _members.ClientSize.Width - memberWidth - roleWidth - 12);
            _members.Columns[0].Width = memberWidth;
            _members.Columns[1].Width = roleWidth;
            _members.Columns[2].Width = systemRoleWidth;
        }

        if (_columns.ClientSize.Width > 0)
        {
            var columnWidth = 240;
            var statusWidth = 140;
            var wipWidth = Math.Max(90, _columns.ClientSize.Width - columnWidth - statusWidth - 12);
            _columns.Columns[0].Width = columnWidth;
            _columns.Columns[1].Width = statusWidth;
            _columns.Columns[2].Width = wipWidth;
        }

        if (_labels.ClientSize.Width > 0)
        {
            var labelWidth = 240;
            _labels.Columns[0].Width = labelWidth;
            _labels.Columns[1].Width = Math.Max(120, _labels.ClientSize.Width - labelWidth - 12);
        }

        if (_components.ClientSize.Width > 0)
        {
            var componentWidth = 210;
            var leadWidth = 160;
            var descriptionWidth = Math.Max(220, _components.ClientSize.Width - componentWidth - leadWidth - 12);
            _components.Columns[0].Width = componentWidth;
            _components.Columns[1].Width = leadWidth;
            _components.Columns[2].Width = descriptionWidth;
        }

        if (_versions.ClientSize.Width > 0)
        {
            var versionWidth = 230;
            var dateWidth = 150;
            var statusWidth = Math.Max(120, _versions.ClientSize.Width - versionWidth - dateWidth - 12);
            _versions.Columns[0].Width = versionWidth;
            _versions.Columns[1].Width = dateWidth;
            _versions.Columns[2].Width = statusWidth;
        }
    }

    private async Task SaveProjectAsync()
    {
        if (_project is null) return;
        try
        {
            await _session.ProjectCommands.UpdateProjectAsync(_project.Id, _name.Text, _description.Text, (ProjectCategory)_category.SelectedItem!, _url.Text);
            await LoadProjectAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task ArchiveProjectAsync()
    {
        if (_project is null) return;
        if (MessageBox.Show(this, $"Archive project '{_project.Name}'? Users will no longer see it in active project lists.", "Archive Project", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            var archived = await _session.ProjectCommands.ArchiveProjectAsync(_project.Id);
            if (!archived)
            {
                ErrorDialogService.Show("The project could not be archived.");
                return;
            }
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task AddMemberAsync()
    {
        if (_project is null) return;
        try
        {
            var candidates = (await _session.UserCommands.GetAllAsync()).Where(x => _project.Members.All(m => m.UserId != x.Id)).ToList();
            using var dialog = new MemberDialog(candidates);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            await _session.ProjectCommands.AddMemberAsync(_project.Id, dialog.SelectedUserId, dialog.SelectedRole);
            await LoadProjectAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task ChangeMemberRoleAsync()
    {
        if (_project is null || SelectedMember is not { } member) return;
        try
        {
            using var dialog = new MemberRoleDialog(member.ProjectRole);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            await _session.ProjectCommands.UpdateMemberRoleAsync(_project.Id, member.UserId, dialog.SelectedRole);
            await LoadProjectAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task RemoveMemberAsync()
    {
        if (_project is null || SelectedMember is not { } member) return;
        if (MessageBox.Show(this, $"Remove {member.User.DisplayName} from this project?", "Remove Member", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            await _session.ProjectCommands.RemoveMemberAsync(_project.Id, member.UserId);
            await LoadProjectAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task EditColumnAsync()
    {
        if (_project is null || SelectedColumn is not { } column) return;
        try
        {
            using var dialog = new BoardColumnDialog(column);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            await _session.ProjectCommands.UpdateBoardColumnAsync(_project.Id, column.Id, dialog.ColumnName, dialog.WipLimit);
            await LoadProjectAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task AddLabelAsync()
    {
        if (_project is null) return;
        try
        {
            using var dialog = new LabelDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            await _session.Labels.CreateAsync(_project.Id, dialog.LabelName, dialog.SelectedColorHex);
            await LoadProjectAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task EditLabelAsync()
    {
        if (SelectedProjectLabel is not { } label) return;
        try
        {
            using var dialog = new LabelDialog(label.Name, label.Color);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            await _session.Labels.UpdateAsync(label.Id, dialog.LabelName, dialog.SelectedColorHex);
            await LoadProjectAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task DeleteLabelAsync()
    {
        if (SelectedProjectLabel is not { } label) return;
        if (MessageBox.Show(this, $"Delete label '{label.Name}'?", "Delete Label", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            await _session.Labels.DeleteAsync(label.Id);
            await LoadProjectAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task AddComponentAsync()
    {
        if (_project is null) return;
        try
        {
            using var dialog = new ComponentDialog(GetLeadCandidates());
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            await _session.Components.CreateAsync(_project.Id, dialog.ComponentName, dialog.DescriptionText, dialog.SelectedLeadUserId);
            await LoadProjectAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task EditComponentAsync()
    {
        if (SelectedProjectComponent is not { } component) return;
        try
        {
            using var dialog = new ComponentDialog(GetLeadCandidates(), component);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            await _session.Components.UpdateAsync(component.Id, dialog.ComponentName, dialog.DescriptionText, dialog.SelectedLeadUserId);
            await LoadProjectAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task DeleteComponentAsync()
    {
        if (SelectedProjectComponent is not { } component) return;
        if (MessageBox.Show(this, $"Delete component '{component.Name}'?", "Delete Component", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            await _session.Components.DeleteAsync(component.Id);
            await LoadProjectAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task AddVersionAsync()
    {
        if (_project is null) return;
        try
        {
            using var dialog = new VersionDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            await _session.Versions.CreateAsync(_project.Id, dialog.VersionName, dialog.DescriptionText, dialog.ReleaseDate);
            await LoadProjectAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task EditVersionAsync()
    {
        if (SelectedProjectVersion is not { } version) return;
        try
        {
            using var dialog = new VersionDialog(version);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            await _session.Versions.UpdateAsync(version.Id, dialog.VersionName, dialog.DescriptionText, dialog.ReleaseDate, dialog.IsReleased);
            await LoadProjectAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task DeleteVersionAsync()
    {
        if (SelectedProjectVersion is not { } version) return;
        if (MessageBox.Show(this, $"Delete version '{version.Name}'?", "Delete Version", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            await _session.Versions.DeleteAsync(version.Id);
            await LoadProjectAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task MarkVersionReleasedAsync()
    {
        if (SelectedProjectVersion is not { } version || version.IsReleased) return;
        try
        {
            await _session.Versions.MarkReleasedAsync(version.Id, version.ReleaseDate ?? DateTime.Today);
            await LoadProjectAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private IReadOnlyList<User> GetLeadCandidates()
    {
        return _project?.Members.Select(x => x.User).OrderBy(x => x.DisplayName).ToList() ?? [];
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
        HideSelection = false,
    };

    private static Label CreateEmptyState(string text)
    {
        var label = JiraControlFactory.CreateLabel(text, true);
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleCenter;
        label.Visible = false;
        return label;
    }

    private static TabPage CreatePage(string text) => new() { Text = text, BackColor = JiraTheme.BgPage };

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.Controls.Add(JiraControlFactory.CreateLabel(label, true), 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private static Label CreateBadgeLabel()
    {
        var label = JiraControlFactory.CreateLabel(string.Empty, true);
        label.AutoSize = true;
        label.BackColor = JiraTheme.Blue100;
        label.ForeColor = JiraTheme.PrimaryActive;
        label.Padding = new Padding(10, 6, 10, 6);
        label.Margin = new Padding(0, 0, 8, 0);
        return label;
    }

    private static void StyleDangerButton(Button button)
    {
        button.ForeColor = JiraTheme.Danger;
        button.FlatAppearance.BorderColor = JiraTheme.Danger;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 241, 240);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(255, 228, 226);
        button.MouseEnter += (_, _) => button.BackColor = Color.FromArgb(255, 241, 240);
        button.MouseLeave += (_, _) => button.BackColor = JiraTheme.BgSurface;
    }

    private static void ConfigureActionButton(Button button, int width)
    {
        button.AutoSize = false;
        button.Width = width;
        button.Height = 40;
        button.MinimumSize = new Size(width, 36);
    }

    private void UpdateActionState()
    {
        var canManage = _session.Authorization.IsInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var hasProject = _project is not null;

        _saveProject.Enabled = canManage && hasProject;
        _archiveProject.Enabled = _session.Authorization.IsInRole(RoleCatalog.Admin) && hasProject;
        _addMember.Enabled = canManage && hasProject;
        _changeMemberRole.Enabled = canManage && SelectedMember is not null;
        _removeMember.Enabled = canManage && SelectedMember is not null;
        _editColumn.Enabled = canManage && SelectedColumn is not null;

        _addLabel.Enabled = canManage && hasProject;
        _editLabel.Enabled = canManage && SelectedProjectLabel is not null;
        _deleteLabel.Enabled = canManage && SelectedProjectLabel is not null;

        _addComponent.Enabled = canManage && hasProject;
        _editComponent.Enabled = canManage && SelectedProjectComponent is not null;
        _deleteComponent.Enabled = canManage && SelectedProjectComponent is not null;

        _addVersion.Enabled = canManage && hasProject;
        _editVersion.Enabled = canManage && SelectedProjectVersion is not null;
        _deleteVersion.Enabled = canManage && SelectedProjectVersion is not null;
        _markVersionReleased.Enabled = canManage && SelectedProjectVersion is { IsReleased: false };
    }

    private sealed class MemberDialog : Form
    {
        private readonly ComboBox _users = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody, Width = 280 };
        private readonly ComboBox _role = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody, Width = 280 };

        public MemberDialog(IReadOnlyList<User> users)
        {
            Text = "Add Member";
            AutoScaleMode = AutoScaleMode.Font;
            Width = 380;
            Height = 230;
            MinimumSize = new Size(380, 230);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = JiraTheme.BgSurface;
            Font = JiraTheme.FontBody;
            _users.DataSource = users.ToList();
            _users.DisplayMember = nameof(User.DisplayName);
            _users.ValueMember = nameof(User.Id);
            _role.DataSource = Enum.GetValues<ProjectRole>();

            var save = JiraControlFactory.CreatePrimaryButton("Add");
            var cancel = JiraControlFactory.CreateSecondaryButton("Cancel");
            save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = JiraTheme.BgSurface };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);

            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16), BackColor = JiraTheme.BgSurface };
            layout.Controls.Add(JiraControlFactory.CreateLabel("User", true));
            layout.Controls.Add(_users);
            layout.Controls.Add(JiraControlFactory.CreateLabel("Project Role", true));
            layout.Controls.Add(_role);
            Controls.Add(layout);
            Controls.Add(buttons);
        }

        public int SelectedUserId => _users.SelectedValue is int userId ? userId : (_users.SelectedItem as User)?.Id ?? throw new InvalidOperationException("A user must be selected.");
        public ProjectRole SelectedRole => (ProjectRole)_role.SelectedItem!;
    }

    private sealed class MemberRoleDialog : Form
    {
        private readonly ComboBox _role = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody, Width = 260 };

        public MemberRoleDialog(ProjectRole currentRole)
        {
            Text = "Change Member Role";
            AutoScaleMode = AutoScaleMode.Font;
            Width = 340;
            Height = 190;
            MinimumSize = new Size(340, 190);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = JiraTheme.BgSurface;
            Font = JiraTheme.FontBody;
            _role.DataSource = Enum.GetValues<ProjectRole>();
            _role.SelectedItem = currentRole;
            var save = JiraControlFactory.CreatePrimaryButton("Save");
            var cancel = JiraControlFactory.CreateSecondaryButton("Cancel");
            save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = JiraTheme.BgSurface };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16), BackColor = JiraTheme.BgSurface };
            layout.Controls.Add(JiraControlFactory.CreateLabel("Project Role", true));
            layout.Controls.Add(_role);
            Controls.Add(layout);
            Controls.Add(buttons);
        }

        public ProjectRole SelectedRole => (ProjectRole)_role.SelectedItem!;
    }

    private sealed class BoardColumnDialog : Form
    {
        private readonly TextBox _name = JiraControlFactory.CreateTextBox();
        private readonly NumericUpDown _wip = new() { Dock = DockStyle.Top, Minimum = 0, Maximum = 99, BorderStyle = BorderStyle.FixedSingle, Font = JiraTheme.FontBody, Width = 260 };

        public BoardColumnDialog(BoardColumn column)
        {
            Text = "Edit Board Column";
            AutoScaleMode = AutoScaleMode.Font;
            Width = 380;
            Height = 230;
            MinimumSize = new Size(380, 230);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = JiraTheme.BgSurface;
            Font = JiraTheme.FontBody;
            _name.Text = column.Name;
            _wip.Value = column.WipLimit ?? 0;
            var save = JiraControlFactory.CreatePrimaryButton("Save");
            var cancel = JiraControlFactory.CreateSecondaryButton("Cancel");
            save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = JiraTheme.BgSurface };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16), BackColor = JiraTheme.BgSurface };
            layout.Controls.Add(JiraControlFactory.CreateLabel("Name", true));
            layout.Controls.Add(_name);
            layout.Controls.Add(JiraControlFactory.CreateLabel("WIP Limit (0 = none)", true));
            layout.Controls.Add(_wip);
            Controls.Add(layout);
            Controls.Add(buttons);
        }

        public string ColumnName => _name.Text;
        public int? WipLimit => _wip.Value == 0 ? null : (int)_wip.Value;
    }

    private sealed class LabelDialog : Form
    {
        private readonly TextBox _name = JiraControlFactory.CreateTextBox();
        private readonly Panel _colorPreview = new() { Width = 44, Height = 28, BackColor = JiraTheme.Primary };
        private readonly Button _pickColor = JiraControlFactory.CreateSecondaryButton("Pick Color");
        private string _selectedColorHex;

        public LabelDialog(string? name = null, string? colorHex = null)
        {
            Text = string.IsNullOrWhiteSpace(name) ? "Add Label" : "Edit Label";
            AutoScaleMode = AutoScaleMode.Font;
            Width = 380;
            Height = 240;
            MinimumSize = new Size(380, 240);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = JiraTheme.BgSurface;
            Font = JiraTheme.FontBody;

            _selectedColorHex = string.IsNullOrWhiteSpace(colorHex) ? "#4688EC" : colorHex;
            _name.Text = name ?? string.Empty;
            _colorPreview.BackColor = ColorTranslator.FromHtml(_selectedColorHex);
            _pickColor.Click += (_, _) => PickColor();

            var save = JiraControlFactory.CreatePrimaryButton("Save");
            var cancel = JiraControlFactory.CreateSecondaryButton("Cancel");
            save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
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
            layout.Controls.Add(JiraControlFactory.CreateLabel("Color", true));
            layout.Controls.Add(colorRow);
            Controls.Add(layout);
            Controls.Add(buttons);
        }

        public string LabelName => _name.Text.Trim();
        public string SelectedColorHex => _selectedColorHex;

        private void PickColor()
        {
            using var dialog = new ColorDialog
            {
                FullOpen = true,
                Color = ColorTranslator.FromHtml(_selectedColorHex)
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            _selectedColorHex = ColorTranslator.ToHtml(dialog.Color);
            _colorPreview.BackColor = dialog.Color;
        }
    }

    private sealed record UserOption(int? Value, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed class ComponentDialog : Form
    {
        private readonly TextBox _name = JiraControlFactory.CreateTextBox();
        private readonly TextBox _description = JiraControlFactory.CreateTextBox();
        private readonly ComboBox _lead = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody, Width = 280 };

        public ComponentDialog(IReadOnlyList<User> users, JiraComponentEntity? component = null)
        {
            Text = component is null ? "Add Component" : "Edit Component";
            AutoScaleMode = AutoScaleMode.Font;
            Width = 420;
            Height = 320;
            MinimumSize = new Size(420, 320);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = JiraTheme.BgSurface;
            Font = JiraTheme.FontBody;

            _name.Text = component?.Name ?? string.Empty;
            _description.Multiline = true;
            _description.Height = 100;
            _description.ScrollBars = ScrollBars.Vertical;
            _description.Text = component?.Description ?? string.Empty;
            _lead.DisplayMember = nameof(UserOption.Text);
            _lead.ValueMember = nameof(UserOption.Value);
            _lead.DataSource = BuildUserOptions(users);
            _lead.SelectedValue = component?.LeadUserId;

            var save = JiraControlFactory.CreatePrimaryButton("Save");
            var cancel = JiraControlFactory.CreateSecondaryButton("Cancel");
            save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = JiraTheme.BgSurface };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);

            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16), BackColor = JiraTheme.BgSurface };
            layout.Controls.Add(JiraControlFactory.CreateLabel("Name", true));
            layout.Controls.Add(_name);
            layout.Controls.Add(JiraControlFactory.CreateLabel("Lead", true));
            layout.Controls.Add(_lead);
            layout.Controls.Add(JiraControlFactory.CreateLabel("Description", true));
            layout.Controls.Add(_description);
            Controls.Add(layout);
            Controls.Add(buttons);
        }

        public string ComponentName => _name.Text.Trim();
        public string? DescriptionText => string.IsNullOrWhiteSpace(_description.Text) ? null : _description.Text.Trim();
        public int? SelectedLeadUserId => _lead.SelectedValue is int userId ? userId : null;

        private static List<UserOption> BuildUserOptions(IReadOnlyList<User> users)
        {
            var options = users.OrderBy(x => x.DisplayName).Select(x => new UserOption(x.Id, x.DisplayName)).ToList();
            options.Insert(0, new UserOption(null, "No lead"));
            return options;
        }
    }

    private sealed class VersionDialog : Form
    {
        private readonly TextBox _name = JiraControlFactory.CreateTextBox();
        private readonly TextBox _description = JiraControlFactory.CreateTextBox();
        private readonly DateTimePicker _releaseDate = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Width = 180, CalendarForeColor = JiraTheme.TextPrimary, CalendarMonthBackground = JiraTheme.BgSurface };
        private readonly CheckBox _released = new() { Text = "Released", AutoSize = true, ForeColor = JiraTheme.TextPrimary, BackColor = JiraTheme.BgSurface, Font = JiraTheme.FontBody };

        public VersionDialog(JiraProjectVersionEntity? version = null)
        {
            Text = version is null ? "Add Version" : "Edit Version";
            AutoScaleMode = AutoScaleMode.Font;
            Width = 420;
            Height = 320;
            MinimumSize = new Size(420, 320);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = JiraTheme.BgSurface;
            Font = JiraTheme.FontBody;

            _name.Text = version?.Name ?? string.Empty;
            _description.Multiline = true;
            _description.Height = 100;
            _description.ScrollBars = ScrollBars.Vertical;
            _description.Text = version?.Description ?? string.Empty;
            _releaseDate.Checked = version?.ReleaseDate is not null;
            _releaseDate.Value = version?.ReleaseDate ?? DateTime.Today;
            _released.Checked = version?.IsReleased ?? false;

            var save = JiraControlFactory.CreatePrimaryButton("Save");
            var cancel = JiraControlFactory.CreateSecondaryButton("Cancel");
            save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = JiraTheme.BgSurface };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);

            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16), BackColor = JiraTheme.BgSurface };
            layout.Controls.Add(JiraControlFactory.CreateLabel("Name", true));
            layout.Controls.Add(_name);
            layout.Controls.Add(JiraControlFactory.CreateLabel("Release date", true));
            layout.Controls.Add(_releaseDate);
            layout.Controls.Add(_released);
            layout.Controls.Add(JiraControlFactory.CreateLabel("Description", true));
            layout.Controls.Add(_description);
            Controls.Add(layout);
            Controls.Add(buttons);
        }

        public string VersionName => _name.Text.Trim();
        public string? DescriptionText => string.IsNullOrWhiteSpace(_description.Text) ? null : _description.Text.Trim();
        public DateTime? ReleaseDate => _releaseDate.Checked ? _releaseDate.Value.Date : null;
        public bool IsReleased => _released.Checked;
    }
}


