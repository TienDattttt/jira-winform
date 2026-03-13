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
    private Project? _project;
    private bool _isLoading;

    public ProjectSettingsForm(AppSession session)
    {
        _session = session;
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;

        _description.Multiline = true;
        _description.Height = 80;
        _category.DataSource = Enum.GetValues<ProjectCategory>();
        _members.Columns.Add("Member", 180);
        _members.Columns.Add("Project Role", 120);
        _members.Columns.Add("System Roles", 220);
        _columns.Columns.Add("Column", 180);
        _columns.Columns.Add("Status", 120);
        _columns.Columns.Add("WIP", 80);

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

        var header = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = JiraTheme.BgPage, Padding = new Padding(16, 16, 16, 8) };
        var title = JiraControlFactory.CreateLabel("Project Settings");
        title.Font = JiraTheme.FontH2;
        var caption = JiraControlFactory.CreateLabel("Manage project details, members, and board columns.", true);
        caption.Location = new Point(0, 36);
        header.Controls.Add(title);
        header.Controls.Add(caption);

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = JiraTheme.FontBody };
        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildMembersTab());
        tabs.TabPages.Add(BuildColumnsTab());

        Controls.Add(tabs);
        Controls.Add(header);
        Load += async (_, _) => await LoadProjectAsync();
    }

    private TabPage BuildGeneralTab()
    {
        var page = CreatePage("General");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20), BackColor = JiraTheme.BgSurface, AutoScroll = true };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(layout, 0, "Name", _name);
        AddRow(layout, 1, "Description", _description);
        AddRow(layout, 2, "Category", _category);
        AddRow(layout, 3, "URL", _url);
        layout.Controls.Add(_saveProject, 1, 4);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildMembersTab()
    {
        var page = CreatePage("Members");
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(12, 8, 12, 10), BackColor = JiraTheme.BgSurface };
        actions.Controls.AddRange([_addMember, _changeMemberRole, _removeMember]);
        page.Controls.Add(_members);
        page.Controls.Add(actions);
        return page;
    }

    private TabPage BuildColumnsTab()
    {
        var page = CreatePage("Board Columns");
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(12, 8, 12, 10), BackColor = JiraTheme.BgSurface };
        actions.Controls.Add(_editColumn);
        page.Controls.Add(_columns);
        page.Controls.Add(actions);
        return page;
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

            _members.Items.Clear();
            foreach (var member in _project.Members.OrderBy(x => x.User.DisplayName))
            {
                var item = new ListViewItem(member.User.DisplayName) { Tag = member };
                item.SubItems.Add(member.ProjectRole.ToString());
                item.SubItems.Add(string.Join(", ", member.User.UserRoles.Select(x => x.Role.Name)));
                _members.Items.Add(item);
            }

            _columns.Items.Clear();
            foreach (var column in _project.BoardColumns.OrderBy(x => x.DisplayOrder))
            {
                var item = new ListViewItem(column.Name) { Tag = column };
                item.SubItems.Add(column.StatusCode.ToString());
                item.SubItems.Add(column.WipLimit?.ToString() ?? "-");
                _columns.Items.Add(item);
            }

            var canManage = _session.Authorization.IsInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
            _saveProject.Enabled = canManage;
            _addMember.Enabled = canManage;
            _changeMemberRole.Enabled = canManage;
            _removeMember.Enabled = canManage;
            _editColumn.Enabled = canManage;
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
        if (_project is null || _members.SelectedItems.Count == 0 || _members.SelectedItems[0].Tag is not ProjectMember member) return;
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
        if (_project is null || _members.SelectedItems.Count == 0 || _members.SelectedItems[0].Tag is not ProjectMember member) return;
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
        if (_project is null || _columns.SelectedItems.Count == 0 || _columns.SelectedItems[0].Tag is not BoardColumn column) return;
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
        Font = JiraTheme.FontBody
    };

    private static TabPage CreatePage(string text) => new() { Text = text, BackColor = JiraTheme.BgSurface };

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.Controls.Add(JiraControlFactory.CreateLabel(label, true), 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private static void ConfigureActionButton(Button button, int width)
    {
        button.AutoSize = false;
        button.Width = width;
        button.Height = 40;
        button.MinimumSize = new Size(width, 36);
    }

    private sealed class MemberDialog : Form
    {
        private readonly ComboBox _users = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody };
        private readonly ComboBox _role = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody };

        public MemberDialog(IReadOnlyList<User> users)
        {
            Text = "Add Member";
            AutoScaleMode = AutoScaleMode.Font;
            Width = 360;
            Height = 180;
            MinimumSize = new Size(360, 180);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = JiraTheme.BgSurface;
            Font = JiraTheme.FontBody;
            _users.DataSource = users.ToList();
            _users.DisplayMember = nameof(User.DisplayName);
            _users.ValueMember = nameof(User.Id);
            _role.DataSource = Enum.GetValues<ProjectRole>();

            var save = JiraControlFactory.CreatePrimaryButton("Add");
            save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16), BackColor = JiraTheme.BgSurface };
            layout.Controls.Add(JiraControlFactory.CreateLabel("User", true));
            layout.Controls.Add(_users);
            layout.Controls.Add(JiraControlFactory.CreateLabel("Project Role", true));
            layout.Controls.Add(_role);
            layout.Controls.Add(save);
            Controls.Add(layout);
        }

        public int SelectedUserId => _users.SelectedValue is int userId ? userId : (_users.SelectedItem as User)?.Id ?? throw new InvalidOperationException("A user must be selected.");
        public ProjectRole SelectedRole => (ProjectRole)_role.SelectedItem!;
    }

    private sealed class MemberRoleDialog : Form
    {
        private readonly ComboBox _role = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody };

        public MemberRoleDialog(ProjectRole currentRole)
        {
            Text = "Change Member Role";
            AutoScaleMode = AutoScaleMode.Font;
            Width = 320;
            Height = 150;
            MinimumSize = new Size(320, 150);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = JiraTheme.BgSurface;
            Font = JiraTheme.FontBody;
            _role.DataSource = Enum.GetValues<ProjectRole>();
            _role.SelectedItem = currentRole;
            var save = JiraControlFactory.CreatePrimaryButton("Save");
            save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16), BackColor = JiraTheme.BgSurface };
            layout.Controls.Add(JiraControlFactory.CreateLabel("Project Role", true));
            layout.Controls.Add(_role);
            layout.Controls.Add(save);
            Controls.Add(layout);
        }

        public ProjectRole SelectedRole => (ProjectRole)_role.SelectedItem!;
    }

    private sealed class BoardColumnDialog : Form
    {
        private readonly TextBox _name = JiraControlFactory.CreateTextBox();
        private readonly NumericUpDown _wip = new() { Dock = DockStyle.Top, Minimum = 0, Maximum = 99, BorderStyle = BorderStyle.FixedSingle, Font = JiraTheme.FontBody };

        public BoardColumnDialog(BoardColumn column)
        {
            Text = "Edit Board Column";
            AutoScaleMode = AutoScaleMode.Font;
            Width = 360;
            Height = 180;
            MinimumSize = new Size(360, 180);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = JiraTheme.BgSurface;
            Font = JiraTheme.FontBody;
            _name.Text = column.Name;
            _wip.Value = column.WipLimit ?? 0;
            var save = JiraControlFactory.CreatePrimaryButton("Save");
            save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16), BackColor = JiraTheme.BgSurface };
            layout.Controls.Add(JiraControlFactory.CreateLabel("Name", true));
            layout.Controls.Add(_name);
            layout.Controls.Add(JiraControlFactory.CreateLabel("WIP Limit (0 = none)", true));
            layout.Controls.Add(_wip);
            layout.Controls.Add(save);
            Controls.Add(layout);
        }

        public string ColumnName => _name.Text;
        public int? WipLimit => _wip.Value == 0 ? null : (int)_wip.Value;
    }
}
