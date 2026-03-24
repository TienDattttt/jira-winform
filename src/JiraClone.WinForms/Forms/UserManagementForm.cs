using JiraClone.Application.Auth;
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
    private readonly ListView _listView = new()
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
    private readonly TextBox _searchBox = JiraControlFactory.CreateTextBox();
    private readonly ComboBox _statusFilter = new()
    {
        Width = 150,
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        BackColor = JiraTheme.BgSurface,
        ForeColor = JiraTheme.TextPrimary,
        Font = JiraTheme.FontBody,
    };
    private readonly Button _createButton = JiraControlFactory.CreatePrimaryButton("Create");
    private readonly Button _editButton = JiraControlFactory.CreateSecondaryButton("Edit");
    private readonly Button _deactivateButton = JiraControlFactory.CreateSecondaryButton("Deactivate");
    private readonly Button _activateButton = JiraControlFactory.CreateSecondaryButton("Activate");
    private readonly Button _resetPasswordButton = JiraControlFactory.CreateSecondaryButton("Reset Password");
    private readonly Label _countBadge = CreateBadgeLabel();
    private readonly Label _emptyState = JiraControlFactory.CreateLabel("No users match the current filters.", true);
    private List<User> _users = [];
    private int _projectId;
    private bool _isLoading;

    public UserManagementForm(AppSession session)
    {
        _session = session;
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;

        JiraTheme.StyleListView(_listView);
        _listView.Columns.Add("Display Name", 220);
        _listView.Columns.Add("User Name", 160);
        _listView.Columns.Add("Email", 260);
        _listView.Columns.Add("Status", 100);
        _listView.Columns.Add("Roles", 280);
        _listView.SelectedIndexChanged += (_, _) => UpdateActionState();
        _listView.DoubleClick += async (_, _) => await EditAsync();

        _statusFilter.Items.AddRange(["All users", "Active only", "Inactive only"]);
        _statusFilter.SelectedIndex = 0;
        _statusFilter.SelectedIndexChanged += (_, _) => BindUsers();
        _searchBox.Width = 240;
        _searchBox.PlaceholderText = "Search users";
        _searchBox.TextChanged += (_, _) => BindUsers();

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

        _emptyState.Dock = DockStyle.Fill;
        _emptyState.TextAlign = ContentAlignment.MiddleCenter;
        _emptyState.Visible = false;

        Controls.Add(BuildSurface());
        Controls.Add(BuildToolbar());
        Controls.Add(BuildHeader());
        Load += async (_, _) => await LoadUsersAsync();
        UpdateActionState();
    }

    private User? SelectedUser => _listView.SelectedItems.Count == 0 ? null : _listView.SelectedItems[0].Tag as User;

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 88,
            BackColor = JiraTheme.BgPage,
            Padding = new Padding(20, 18, 20, 8),
        };

        var title = JiraControlFactory.CreateLabel("Users");
        title.Font = JiraTheme.FontH1;
        title.Location = new Point(0, 0);

        var caption = JiraControlFactory.CreateLabel("Manage user access, lifecycle, and project membership in one place.", true);
        caption.Location = new Point(0, 42);

        header.Controls.Add(title);
        header.Controls.Add(caption);
        header.Controls.Add(_countBadge);
        _countBadge.Location = new Point(110, 10);
        return header;
    }

    private Control BuildToolbar()
    {
        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = JiraTheme.BgPage,
            Padding = new Padding(16, 8, 16, 8),
        };
        var strip = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            AutoScroll = false,
            BackColor = JiraTheme.BgPage,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        _searchBox.Margin = new Padding(0, 0, 12, 0);
        _statusFilter.Margin = new Padding(0, 0, 16, 0);
        _createButton.Margin = new Padding(0, 0, 8, 0);
        _editButton.Margin = new Padding(0, 0, 8, 0);
        _deactivateButton.Margin = new Padding(0, 0, 8, 0);
        _activateButton.Margin = new Padding(0, 0, 8, 0);
        _resetPasswordButton.Margin = new Padding(0);

        strip.Controls.Add(_searchBox);
        strip.Controls.Add(_statusFilter);
        strip.Controls.Add(_createButton);
        strip.Controls.Add(_editButton);
        strip.Controls.Add(_deactivateButton);
        strip.Controls.Add(_activateButton);
        strip.Controls.Add(_resetPasswordButton);

        toolbar.Controls.Add(strip);
        return toolbar;
    }
    private Control BuildSurface()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 0, 20, 20),
            BackColor = JiraTheme.BgPage,
        };

        var surface = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
        };
        surface.Paint += (_, e) =>
        {
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, surface.Width - 1, surface.Height - 1);
        };

        surface.Controls.Add(_emptyState);
        surface.Controls.Add(_listView);
        host.Controls.Add(surface);
        return host;
    }

    public Task RefreshUsersAsync(CancellationToken cancellationToken = default) => LoadUsersAsync(cancellationToken);

    public void SetShellSearch(string value)
    {
        var target = value ?? string.Empty;
        if (string.Equals(_searchBox.Text, target, StringComparison.Ordinal))
        {
            return;
        }

        _searchBox.Text = target;
    }

    private async Task LoadUsersAsync(CancellationToken cancellationToken = default)
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
                project = await _session.Projects.GetActiveProjectAsync(cancellationToken);
                if (project is null)
                {
                    return;
                }

                users = (await _session.UserCommands.GetAllAsync(cancellationToken)).OrderBy(x => x.DisplayName).ToList();
            }, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            if (project is null)
            {
                return;
            }

            _projectId = project.Id;
            _users = users;
            BindUsers();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
        finally
        {
            _isLoading = false;
            UpdateActionState();
        }
    }

    private void BindUsers()
    {
        var search = _searchBox.Text.Trim();
        var statusIndex = _statusFilter.SelectedIndex;
        var filtered = _users
            .Where(user => statusIndex switch
            {
                1 => user.IsActive,
                2 => !user.IsActive,
                _ => true,
            })
            .Where(user =>
                string.IsNullOrWhiteSpace(search) ||
                user.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                user.UserName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                user.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                user.UserRoles.Any(x => x.Role.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        _listView.BeginUpdate();
        _listView.Items.Clear();
        foreach (var user in filtered)
        {
            var item = new ListViewItem(user.DisplayName) { Tag = user };
            item.SubItems.Add(user.UserName);
            item.SubItems.Add(user.Email);
            item.SubItems.Add(user.IsActive ? "Active" : "Inactive");
            item.SubItems.Add(string.Join(", ", user.UserRoles.Select(x => x.Role.Name)));
            _listView.Items.Add(item);
        }
        _listView.EndUpdate();

        _countBadge.Text = filtered.Count == 1 ? "1 user" : $"{filtered.Count} users";
        _emptyState.Visible = filtered.Count == 0;
        _listView.Visible = filtered.Count > 0;
        UpdateActionState();
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
        try
        {
            if (SelectedUser is null)
            {
                ErrorDialogService.Show("Select a user first.");
                return;
            }

            await _session.UserCommands.DeactivateAsync(SelectedUser.Id);
            await LoadUsersAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task ActivateAsync()
    {
        try
        {
            if (SelectedUser is null)
            {
                ErrorDialogService.Show("Select a user first.");
                return;
            }

            await _session.UserCommands.ActivateAsync(SelectedUser.Id);
            await LoadUsersAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task ResetPasswordAsync()
    {
        try
        {
            if (SelectedUser is null)
            {
                ErrorDialogService.Show("Select a user first.");
                return;
            }

            using var dialog = new ResetPasswordDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            await _session.UserCommands.ResetPasswordAsync(SelectedUser.Id, dialog.NewPassword);
            MessageBox.Show(this, "Password has been reset.", "User Management", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private static Label CreateBadgeLabel()
    {
        var label = JiraControlFactory.CreateLabel("0 users", true);
        label.AutoSize = true;
        label.BackColor = JiraTheme.Blue100;
        label.ForeColor = JiraTheme.PrimaryActive;
        label.Padding = new Padding(10, 6, 10, 6);
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
        var isAdmin = _session.Authorization.IsInRole(RoleCatalog.Admin);
        var selectedUser = SelectedUser;
        var hasSelection = selectedUser is not null;
        // Always show T?o for admin - no project dependency
        _createButton.Visible = isAdmin;
        _createButton.Enabled = isAdmin;
        // Show action buttons when a row is selected
        _editButton.Visible = isAdmin && hasSelection;
        _editButton.Enabled = _editButton.Visible;
        _resetPasswordButton.Visible = isAdmin && hasSelection;
        _resetPasswordButton.Enabled = _resetPasswordButton.Visible;
        _deactivateButton.Visible = isAdmin && (selectedUser?.IsActive == true);
        _deactivateButton.Enabled = _deactivateButton.Visible;
        _activateButton.Visible = isAdmin && (selectedUser?.IsActive == false);
        _activateButton.Enabled = _activateButton.Visible;
    }

    private sealed class UserEditorDialog : Form
    {
        private readonly TextBox _userName = JiraControlFactory.CreateTextBox();
        private readonly TextBox _displayName = JiraControlFactory.CreateTextBox();
        private readonly TextBox _email = JiraControlFactory.CreateTextBox();
        private readonly TextBox _password = JiraControlFactory.CreateTextBox();
        private readonly Label _passwordError = JiraControlFactory.CreateLabel(string.Empty, true);
        private readonly ComboBox _projectRole = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody };
        private readonly CheckedListBox _roles = new() { Dock = DockStyle.Fill, Height = 120, BorderStyle = BorderStyle.None, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody };
        private readonly CheckBox _isActive = new() { Text = "Active", Checked = true, AutoSize = true, Font = JiraTheme.FontBody, ForeColor = JiraTheme.TextPrimary };
        private readonly Button _saveButton = JiraControlFactory.CreatePrimaryButton("Save");
        private readonly bool _isCreateMode;

        public UserEditorDialog(IReadOnlyList<Role> roles, User? user, ProjectRole projectRole = ProjectRole.Developer)
        {
            _isCreateMode = user is null;
            Text = _isCreateMode ? "Create User" : "Edit User";
            AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
            Width = 560;
            Height = _isCreateMode ? 520 : 470;
            MinimumSize = new Size(560, 470);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = JiraTheme.BgSurface;
            Font = JiraTheme.FontBody;

            _password.UseSystemPasswordChar = true;
            _password.PlaceholderText = "Leave blank to use ChangeMe123!";
            _password.TextChanged += (_, _) => UpdatePasswordValidation();
            _passwordError.ForeColor = JiraTheme.Red600;
            _passwordError.MaximumSize = new Size(320, 0);
            _passwordError.Visible = false;

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

            _saveButton.Click += (_, _) =>
            {
                if (_saveButton.Enabled)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            var cancel = JiraControlFactory.CreateSecondaryButton("Cancel");
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = JiraTheme.BgSurface };
            buttons.Controls.Add(_saveButton);
            buttons.Controls.Add(cancel);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16), BackColor = JiraTheme.BgSurface, AutoScroll = true, RowCount = _isCreateMode ? 8 : 6 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var row = 0;
            AddRow(layout, row++, "User Name", _userName);
            AddRow(layout, row++, "Display Name", _displayName);
            AddRow(layout, row++, "Email", _email);

            if (_isCreateMode)
            {
                AddRow(layout, row++, "Password", _password);
                layout.Controls.Add(_passwordError, 1, row++);
            }

            AddRow(layout, row++, "Project Role", _projectRole);
            AddRow(layout, row++, "Roles", _roles);
            layout.Controls.Add(_isActive, 1, row);

            Controls.Add(layout);
            Controls.Add(buttons);

            AcceptButton = _saveButton;
            CancelButton = cancel;
            UpdatePasswordValidation();
        }

        public string UserName => _userName.Text;
        public string DisplayName => _displayName.Text;
        public string Email => _email.Text;
        public string Password => string.IsNullOrWhiteSpace(_password.Text) ? "ChangeMe123!" : _password.Text;
        public ProjectRole ProjectRole => (ProjectRole)_projectRole.SelectedItem!;
        public IReadOnlyCollection<string> SelectedRoles => _roles.CheckedItems.Cast<string>().ToArray();
        public bool IsActive => _isActive.Checked;

        private void UpdatePasswordValidation()
        {
            if (!_isCreateMode)
            {
                _passwordError.Visible = false;
                _saveButton.Enabled = true;
                return;
            }

            var error = AuthenticationService.GetPasswordValidationError(Password);
            _passwordError.Text = error ?? string.Empty;
            _passwordError.Visible = !string.IsNullOrWhiteSpace(error);
            _saveButton.Enabled = string.IsNullOrWhiteSpace(error);
        }

        private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
        {
            layout.Controls.Add(JiraControlFactory.CreateLabel(label, true), 0, row);
            layout.Controls.Add(control, 1, row);
        }
    }
}





