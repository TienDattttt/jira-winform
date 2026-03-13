using JiraClone.Application.Roles;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

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
    private readonly Button _saveProject = JiraControlFactory.CreatePrimaryButton("Save Project");
    private readonly Button _addMember = JiraControlFactory.CreateSecondaryButton("Add Member");
    private readonly Button _changeMemberRole = JiraControlFactory.CreateSecondaryButton("Change Role");
    private readonly Button _removeMember = JiraControlFactory.CreateSecondaryButton("Remove Member");
    private readonly Button _editColumn = JiraControlFactory.CreateSecondaryButton("Edit Column");
    private readonly Label _memberCountBadge = CreateBadgeLabel();
    private readonly Label _columnCountBadge = CreateBadgeLabel();
    private readonly Label _membersEmptyState = JiraControlFactory.CreateLabel("No members on this project yet.", true);
    private readonly Label _columnsEmptyState = JiraControlFactory.CreateLabel("No board columns configured.", true);
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

        JiraTheme.StyleListView(_members);
        JiraTheme.StyleListView(_columns);
        _members.Columns.Add("Member", 220);
        _members.Columns.Add("Project Role", 140);
        _members.Columns.Add("System Roles", 280);
        _columns.Columns.Add("Column", 220);
        _columns.Columns.Add("Status", 140);
        _columns.Columns.Add("WIP", 100);
        _members.SelectedIndexChanged += (_, _) => UpdateActionState();
        _columns.SelectedIndexChanged += (_, _) => UpdateActionState();
        _members.Resize += (_, _) => ApplyResponsiveColumns();
        _columns.Resize += (_, _) => ApplyResponsiveColumns();

        _saveProject.Click += async (_, _) => await SaveProjectAsync();
        _addMember.Click += async (_, _) => await AddMemberAsync();
        _changeMemberRole.Click += async (_, _) => await ChangeMemberRoleAsync();
        _removeMember.Click += async (_, _) => await RemoveMemberAsync();
        _editColumn.Click += async (_, _) => await EditColumnAsync();
        ConfigureActionButton(_saveProject, 118);
        ConfigureActionButton(_addMember, 116);
        ConfigureActionButton(_changeMemberRole, 116);
        ConfigureActionButton(_removeMember, 126);
        ConfigureActionButton(_editColumn, 104);

        _membersEmptyState.Dock = DockStyle.Fill;
        _membersEmptyState.TextAlign = ContentAlignment.MiddleCenter;
        _membersEmptyState.Visible = false;
        _columnsEmptyState.Dock = DockStyle.Fill;
        _columnsEmptyState.TextAlign = ContentAlignment.MiddleCenter;
        _columnsEmptyState.Visible = false;

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = JiraTheme.FontBody };
        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildMembersTab());
        tabs.TabPages.Add(BuildColumnsTab());

        Controls.Add(tabs);
        Controls.Add(BuildHeader());
        Load += async (_, _) => await LoadProjectAsync();
        UpdateActionState();
    }

    private ProjectMember? SelectedMember => _members.SelectedItems.Count == 0 ? null : _members.SelectedItems[0].Tag as ProjectMember;
    private BoardColumn? SelectedColumn => _columns.SelectedItems.Count == 0 ? null : _columns.SelectedItems[0].Tag as BoardColumn;

    public Task RefreshProjectAsync() => LoadProjectAsync();

    public void SetShellSearch(string value)
    {
        _shellSearch = value?.Trim() ?? string.Empty;
        BindProjectLists();
    }

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = JiraTheme.BgPage, Padding = new Padding(20, 18, 20, 8) };
        var title = JiraControlFactory.CreateLabel("Project Settings");
        title.Font = JiraTheme.FontH1;
        title.Location = new Point(0, 0);

        var caption = JiraControlFactory.CreateLabel("Adjust project details, members, and board structure without leaving the desktop flow.", true);
        caption.Location = new Point(0, 42);

        var badges = new FlowLayoutPanel
        {
            Location = new Point(0, 70),
            AutoSize = true,
            WrapContents = false,
            BackColor = JiraTheme.BgPage,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        badges.Controls.AddRange([_memberCountBadge, _columnCountBadge]);

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
        layout.Controls.Add(_saveProject, 1, 4);
        page.Controls.Add(WrapSurface(layout));
        return page;
    }

    private TabPage BuildMembersTab()
    {
        var page = CreatePage("Members");
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(12, 8, 12, 10), BackColor = JiraTheme.BgSurface };
        actions.Controls.AddRange([_addMember, _changeMemberRole, _removeMember]);

        var surface = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
        surface.Controls.Add(_membersEmptyState);
        surface.Controls.Add(_members);
        surface.Controls.Add(actions);
        page.Controls.Add(WrapSurface(surface));
        return page;
    }

    private TabPage BuildColumnsTab()
    {
        var page = CreatePage("Board Columns");
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(12, 8, 12, 10), BackColor = JiraTheme.BgSurface };
        actions.Controls.Add(_editColumn);

        var surface = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
        surface.Controls.Add(_columnsEmptyState);
        surface.Controls.Add(_columns);
        surface.Controls.Add(actions);
        page.Controls.Add(WrapSurface(surface));
        return page;
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
        ApplyResponsiveColumns();

        _membersEmptyState.Visible = members.Count == 0;
        _members.Visible = members.Count > 0;
        _columnsEmptyState.Visible = columns.Count == 0;
        _columns.Visible = columns.Count > 0;
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
    }

    private async Task SaveProjectAsync()
    {
        if (_project is null) return;
        try
        {
            await _session.ProjectCommands.UpdateProjectAsync(_project.Id, _name.Text, _description.Text, (ProjectCategory)_category.SelectedItem!, _url.Text);
            await LoadProjectAsync();
        }
        catch (Exception exception) { ErrorDialogService.Show(exception); }
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
        catch (Exception exception) { ErrorDialogService.Show(exception); }
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
        catch (Exception exception) { ErrorDialogService.Show(exception); }
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
        catch (Exception exception) { ErrorDialogService.Show(exception); }
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
        catch (Exception exception) { ErrorDialogService.Show(exception); }
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
        var hasMemberSelection = SelectedMember is not null;
        var hasColumnSelection = SelectedColumn is not null;

        _saveProject.Enabled = canManage && hasProject;
        _addMember.Enabled = canManage && hasProject;
        _changeMemberRole.Enabled = canManage && hasMemberSelection;
        _removeMember.Enabled = canManage && hasMemberSelection;
        _editColumn.Enabled = canManage && hasColumnSelection;
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
}



