using JiraClone.Application.Roles;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public class UserManagementForm : UserControl
{
    private readonly AppSession _session;
    private readonly ListView _listView = new() { Dock = DockStyle.Fill, FullRowSelect = true, MultiSelect = false, View = View.Details, BorderStyle = BorderStyle.None, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody };
    private readonly Button _createButton = JiraControlFactory.CreatePrimaryButton("Create");
    private readonly Button _editButton = JiraControlFactory.CreateSecondaryButton("Edit");
    private readonly Button _deactivateButton = JiraControlFactory.CreateSecondaryButton("Deactivate");
    private readonly Button _activateButton = JiraControlFactory.CreateSecondaryButton("Activate");
    private readonly Button _resetPasswordButton = JiraControlFactory.CreateSecondaryButton("Reset Password");
    private List<User> _users = [];
    private int _projectId;
    private bool _isLoading;

    public UserManagementForm(AppSession session)
    {
        _session = session;
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;

        _listView.Columns.Add("Display Name", 180);
        _listView.Columns.Add("User Name", 140);
        _listView.Columns.Add("Email", 220);
        _listView.Columns.Add("Status", 80);
        _listView.Columns.Add("Roles", 220);

        _createButton.Click += async (_, _) => await CreateAsync();
        _editButton.Click += async (_, _) => await EditAsync();
        _deactivateButton.Click += async (_, _) => await DeactivateAsync();
        _activateButton.Click += async (_, _) => await ActivateAsync();
        _resetPasswordButton.Click += async (_, _) => await ResetPasswordAsync();
        ConfigureActionButton(_createButton, 98);
        ConfigureActionButton(_editButton, 88);
        ConfigureActionButton(_deactivateButton, 112);
        ConfigureActionButton(_activateButton, 98);
        ConfigureActionButton(_resetPasswordButton, 132);

        var header = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = JiraTheme.BgPage, Padding = new Padding(16, 16, 16, 8) };
        var title = JiraControlFactory.CreateLabel("Users");
        title.Font = JiraTheme.FontH2;
        var caption = JiraControlFactory.CreateLabel("Manage users, roles, and access.", true);
        caption.Location = new Point(0, 36);
        header.Controls.Add(title);
        header.Controls.Add(caption);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 62, Padding = new Padding(12, 8, 12, 10), BackColor = JiraTheme.BgPage };
        actions.Controls.AddRange([_createButton, _editButton, _deactivateButton, _activateButton, _resetPasswordButton]);

        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 0, 16, 16), BackColor = JiraTheme.BgPage };
        host.Controls.Add(_listView);

        Controls.Add(host);
        Controls.Add(actions);
        Controls.Add(header);
        Load += async (_, _) => await LoadUsersAsync();
    }

    private User? SelectedUser => _listView.SelectedIndices.Count == 0 ? null : _users[_listView.SelectedIndices[0]];

    private async Task LoadUsersAsync()
    {
        if (_isLoading || !Visible)
        {
            return;
        }

        try
        {
            _isLoading = true;
            Project? project = null;
            List<User> users = [];

            await _session.RunSerializedAsync(async () =>
            {
                project = await _session.Projects.GetActiveProjectAsync();
                if (project is null)
                {
                    return;
                }

                users = (await _session.UserCommands.GetAllAsync()).ToList();
            });

            if (project is null)
            {
                return;
            }

            _projectId = project.Id;
            _users = users;
            _listView.Items.Clear();
            foreach (var user in _users)
            {
                var item = new ListViewItem(user.DisplayName);
                item.SubItems.Add(user.UserName);
                item.SubItems.Add(user.Email);
                item.SubItems.Add(user.IsActive ? "Active" : "Inactive");
                item.SubItems.Add(string.Join(", ", user.UserRoles.Select(x => x.Role.Name)));
                _listView.Items.Add(item);
            }

            var isAdmin = _session.Authorization.IsInRole(RoleCatalog.Admin);
            _createButton.Enabled = isAdmin;
            _editButton.Enabled = isAdmin;
            _deactivateButton.Enabled = isAdmin;
            _activateButton.Enabled = isAdmin;
            _resetPasswordButton.Enabled = isAdmin;
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

    private async Task CreateAsync()
    {
        try
        {
            using var dialog = new UserEditorDialog(await _session.UserCommands.GetRolesAsync(), null);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            await _session.UserCommands.CreateAsync(_projectId, dialog.UserName, dialog.DisplayName, dialog.Email, dialog.Password, dialog.ProjectRole, dialog.SelectedRoles);
            await LoadUsersAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task EditAsync()
    {
        try
        {
            var user = SelectedUser;
            if (user is null)
            {
                ErrorDialogService.Show("Select a user first.");
                return;
            }

            var projectRole = user.ProjectMemberships.FirstOrDefault(x => x.ProjectId == _projectId)?.ProjectRole ?? ProjectRole.Developer;
            using var dialog = new UserEditorDialog(await _session.UserCommands.GetRolesAsync(), user, projectRole);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            await _session.UserCommands.UpdateAsync(user.Id, dialog.DisplayName, dialog.Email, dialog.IsActive, dialog.ProjectRole, dialog.SelectedRoles);
            await LoadUsersAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task DeactivateAsync()
    {
        if (SelectedUser is null)
        {
            return;
        }

        await _session.UserCommands.DeactivateAsync(SelectedUser.Id);
        await LoadUsersAsync();
    }

    private async Task ActivateAsync()
    {
        if (SelectedUser is null)
        {
            return;
        }

        await _session.UserCommands.ActivateAsync(SelectedUser.Id);
        await LoadUsersAsync();
    }

    private async Task ResetPasswordAsync()
    {
        if (SelectedUser is null)
        {
            return;
        }

        var password = Microsoft.VisualBasic.Interaction.InputBox("Enter the new password", "Reset Password", "ChangeMe123!");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        await _session.UserCommands.ResetPasswordAsync(SelectedUser.Id, password);
        MessageBox.Show(this, "Password has been reset.", "User Management", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static void ConfigureActionButton(Button button, int width)
    {
        button.AutoSize = false;
        button.Width = width;
        button.Height = 40;
        button.MinimumSize = new Size(width, 36);
    }

    private sealed class UserEditorDialog : Form
    {
        private readonly TextBox _userName = JiraControlFactory.CreateTextBox();
        private readonly TextBox _displayName = JiraControlFactory.CreateTextBox();
        private readonly TextBox _email = JiraControlFactory.CreateTextBox();
        private readonly TextBox _password = JiraControlFactory.CreateTextBox();
        private readonly ComboBox _projectRole = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody };
        private readonly CheckedListBox _roles = new() { Dock = DockStyle.Fill, Height = 120, BorderStyle = BorderStyle.None, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody };
        private readonly CheckBox _isActive = new() { Text = "Active", Checked = true, AutoSize = true, Font = JiraTheme.FontBody, ForeColor = JiraTheme.TextPrimary };

        public UserEditorDialog(IReadOnlyList<Role> roles, User? user, ProjectRole projectRole = ProjectRole.Developer)
        {
            Text = user is null ? "Create User" : "Edit User";
            AutoScaleMode = AutoScaleMode.Font;
            Width = 520;
            Height = 430;
            MinimumSize = new Size(520, 430);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = JiraTheme.BgSurface;
            Font = JiraTheme.FontBody;

            _password.UseSystemPasswordChar = true;
            _projectRole.DataSource = Enum.GetValues<ProjectRole>();
            _projectRole.SelectedItem = projectRole;
            foreach (var role in roles)
            {
                var index = _roles.Items.Add(role.Name);
                if (user?.UserRoles.Any(x => x.Role.Name == role.Name) == true)
                {
                    _roles.SetItemChecked(index, true);
                }
            }

            if (user is not null)
            {
                _userName.Text = user.UserName;
                _userName.ReadOnly = true;
                _displayName.Text = user.DisplayName;
                _email.Text = user.Email;
                _isActive.Checked = user.IsActive;
            }

            var save = JiraControlFactory.CreatePrimaryButton("Save");
            var cancel = JiraControlFactory.CreateSecondaryButton("Cancel");
            save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = JiraTheme.BgSurface };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16), BackColor = JiraTheme.BgSurface, AutoScroll = true };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddRow(layout, 0, "User Name", _userName);
            AddRow(layout, 1, "Display Name", _displayName);
            AddRow(layout, 2, "Email", _email);
            AddRow(layout, 3, "Password", _password);
            AddRow(layout, 4, "Project Role", _projectRole);
            AddRow(layout, 5, "Roles", _roles);
            layout.Controls.Add(_isActive, 1, 6);

            Controls.Add(layout);
            Controls.Add(buttons);
        }

        public string UserName => _userName.Text;
        public string DisplayName => _displayName.Text;
        public string Email => _email.Text;
        public string Password => string.IsNullOrWhiteSpace(_password.Text) ? "ChangeMe123!" : _password.Text;
        public ProjectRole ProjectRole => (ProjectRole)_projectRole.SelectedItem!;
        public IReadOnlyCollection<string> SelectedRoles => _roles.CheckedItems.Cast<string>().ToArray();
        public bool IsActive => _isActive.Checked;

        private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
        {
            layout.Controls.Add(JiraControlFactory.CreateLabel(label, true), 0, row);
            layout.Controls.Add(control, 1, row);
        }
    }
}
