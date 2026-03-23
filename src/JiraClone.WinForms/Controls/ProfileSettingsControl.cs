using JiraClone.Domain.Entities;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Forms;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public sealed class ProfileSettingsControl : UserControl
{
    private readonly AppSession _session;
    private readonly CheckBox _emailNotifications = new()
    {
        AutoSize = true,
        Text = "Email notifications",
        ForeColor = JiraTheme.TextPrimary,
        BackColor = JiraTheme.BgSurface,
        Font = JiraTheme.FontBody,
        Margin = new Padding(0, 8, 0, 0)
    };
    private readonly Button _saveButton = JiraControlFactory.CreatePrimaryButton("Save Preferences");
    private readonly Button _createTokenButton = JiraControlFactory.CreateSecondaryButton("Create New Token");
    private readonly Label _status = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly Label _tokensEmptyState = JiraControlFactory.CreateLabel("No API tokens created yet.", true);
    private readonly DataGridView _tokensGrid = new();
    private readonly List<ApiToken> _tokens = [];
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private CancellationTokenSource? _loadCts;

    public ProfileSettingsControl(AppSession session)
    {
        _session = session;
        Dock = DockStyle.Fill;
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;

        _saveButton.AutoSize = false;
        _saveButton.Size = new Size(196, 40);
        _saveButton.MinimumSize = new Size(196, 40);
        _saveButton.Click += OnSaveButtonClick;

        _createTokenButton.AutoSize = false;
        _createTokenButton.Size = new Size(196, 40);
        _createTokenButton.MinimumSize = new Size(196, 40);
        _createTokenButton.Click += OnCreateTokenButtonClick;

        _status.AutoSize = true;
        _status.ForeColor = JiraTheme.TextSecondary;
        _status.Margin = new Padding(0, 6, 0, 0);
        _status.MaximumSize = new Size(760, 0);

        ConfigureTokenGrid();
        Controls.Add(BuildLayout());
    }

    public Task RefreshProfileAsync(CancellationToken cancellationToken = default) =>
        ExecuteExclusiveAsync(LoadProfileCoreAsync, RestartLoadCancellation(cancellationToken));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelPendingLoad();
            _disposeCts.Cancel();
            _saveButton.Click -= OnSaveButtonClick;
            _createTokenButton.Click -= OnCreateTokenButtonClick;
            _tokensGrid.CellContentClick -= OnTokensGridCellContentClick;
            _operationGate.Dispose();
            _disposeCts.Dispose();
        }

        base.Dispose(disposing);
    }

    private CancellationToken RestartLoadCancellation(CancellationToken cancellationToken = default)
    {
        CancelPendingLoad();
        _loadCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, cancellationToken);
        return _loadCts.Token;
    }

    private CancellationTokenSource CreateOperationSource(CancellationToken cancellationToken = default) =>
        CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, cancellationToken);

    private async Task ExecuteExclusiveAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            await operation(cancellationToken);
        }
        finally
        {
            _operationGate.Release();
        }
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

    private async void OnSaveButtonClick(object? sender, EventArgs e)
    {
        using var operationSource = CreateOperationSource();
        try
        {
            await ExecuteExclusiveAsync(SaveCoreAsync, operationSource.Token);
        }
        catch (OperationCanceledException) when (operationSource.IsCancellationRequested)
        {
        }
    }

    private async void OnCreateTokenButtonClick(object? sender, EventArgs e)
    {
        using var operationSource = CreateOperationSource();
        try
        {
            await ExecuteExclusiveAsync(CreateTokenCoreAsync, operationSource.Token);
        }
        catch (OperationCanceledException) when (operationSource.IsCancellationRequested)
        {
        }
    }

    private async void OnTokensGridCellContentClick(object? sender, DataGridViewCellEventArgs eventArgs)
    {
        using var operationSource = CreateOperationSource();
        try
        {
            await ExecuteExclusiveAsync(ct => HandleTokenGridCellContentClickCoreAsync(eventArgs, ct), operationSource.Token);
        }
        catch (OperationCanceledException) when (operationSource.IsCancellationRequested)
        {
        }
    }

    private Control BuildLayout()
    {
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(28, 24, 28, 24),
            BackColor = JiraTheme.BgSurface,
            AutoScroll = true,
        };

        var title = JiraControlFactory.CreateLabel("Profile");
        title.Font = JiraTheme.FontH2;
        title.Margin = new Padding(0, 0, 0, 6);

        var description = JiraControlFactory.CreateLabel(
            "Choose how Jira Desktop should notify you about assignments, comments, sprint updates, and other changes.",
            true);
        description.MaximumSize = new Size(760, 0);
        description.AutoSize = true;
        description.Margin = new Padding(0, 0, 0, 10);

        var hint = JiraControlFactory.CreateLabel(
            "When enabled, in-app notifications will also send email if SMTP is configured and your account has an email address.",
            true);
        hint.MaximumSize = new Size(760, 0);
        hint.AutoSize = true;
        hint.Margin = new Padding(0, 0, 0, 12);

        var actionRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 12, 0, 20),
            BackColor = JiraTheme.BgSurface,
        };
        actionRow.Controls.Add(_saveButton);

        var divider = new Panel
        {
            Width = 820,
            Height = 1,
            BackColor = JiraTheme.Border,
            Margin = new Padding(0, 12, 0, 18),
        };

        var tokenTitle = JiraControlFactory.CreateLabel("API Tokens");
        tokenTitle.Font = JiraTheme.FontH2;
        tokenTitle.Margin = new Padding(0, 0, 0, 6);

        var tokenDescription = JiraControlFactory.CreateLabel(
            "Create personal access tokens for local tools and integrations. Raw tokens are shown once and stored only as SHA-256 hashes.",
            true);
        tokenDescription.MaximumSize = new Size(760, 0);
        tokenDescription.AutoSize = true;
        tokenDescription.Margin = new Padding(0, 0, 0, 10);

        var tokenActions = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 12),
            BackColor = JiraTheme.BgSurface,
        };
        tokenActions.Controls.Add(_createTokenButton);

        var tokenHost = new Panel
        {
            Width = 860,
            Height = 280,
            BackColor = JiraTheme.BgSurface,
            Margin = new Padding(0),
        };
        tokenHost.Controls.Add(_tokensGrid);
        tokenHost.Controls.Add(_tokensEmptyState);

        layout.Controls.Add(title);
        layout.Controls.Add(description);
        layout.Controls.Add(hint);
        layout.Controls.Add(_emailNotifications);
        layout.Controls.Add(actionRow);
        layout.Controls.Add(divider);
        layout.Controls.Add(tokenTitle);
        layout.Controls.Add(tokenDescription);
        layout.Controls.Add(tokenActions);
        layout.Controls.Add(tokenHost);
        layout.Controls.Add(_status);
        return layout;
    }

    private void ConfigureTokenGrid()
    {
        JiraTheme.StyleDataGridView(_tokensGrid);
        _tokensGrid.Dock = DockStyle.Fill;
        _tokensGrid.ReadOnly = true;
        _tokensGrid.MultiSelect = false;
        _tokensGrid.AllowUserToAddRows = false;
        _tokensGrid.AllowUserToDeleteRows = false;
        _tokensGrid.AllowUserToResizeRows = false;
        _tokensGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _tokensGrid.AutoGenerateColumns = false;
        _tokensGrid.RowHeadersVisible = false;
        _tokensGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", Width = 160 });
        _tokensGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Scopes", Width = 220 });
        _tokensGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Created", Width = 130 });
        _tokensGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Last Used", Width = 130 });
        _tokensGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Expires", Width = 130 });
        _tokensGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", Width = 90 });
        _tokensGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = string.Empty, Text = "Revoke", UseColumnTextForButtonValue = true, Width = 88 });
        _tokensGrid.CellContentClick += OnTokensGridCellContentClick;

        _tokensEmptyState.Dock = DockStyle.Fill;
        _tokensEmptyState.TextAlign = ContentAlignment.MiddleCenter;
        _tokensEmptyState.Visible = false;
    }

    private async Task LoadProfileCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentUser = _session.CurrentUserContext.CurrentUser;
            _emailNotifications.Enabled = currentUser is not null;
            _saveButton.Enabled = currentUser is not null;
            _createTokenButton.Enabled = currentUser is not null;
            _emailNotifications.Checked = currentUser?.EmailNotificationsEnabled ?? false;
            _status.Text = currentUser is null ? "Log in to manage profile preferences and API tokens." : string.Empty;
            _status.ForeColor = JiraTheme.TextSecondary;

            if (currentUser is null)
            {
                BindTokens([]);
                return;
            }

            var tokens = await _session.RunSerializedAsync(
                () => _session.ApiTokens.GetUserTokensAsync(currentUser.Id, cancellationToken),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            BindTokens(tokens);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!IsDisposed)
            {
                ErrorDialogService.Show(exception);
            }
        }
    }

    private void BindTokens(IReadOnlyList<ApiToken> tokens)
    {
        _tokens.Clear();
        _tokens.AddRange(tokens);
        _tokensGrid.Rows.Clear();
        foreach (var token in tokens)
        {
            _tokensGrid.Rows.Add(
                token.Name,
                token.Scopes.Count == 0 ? "-" : string.Join(", ", token.Scopes),
                token.CreatedAtUtc.ToLocalTime().ToString("dd MMM yyyy"),
                token.LastUsedAtUtc.HasValue ? token.LastUsedAtUtc.Value.ToLocalTime().ToString("dd MMM yyyy HH:mm") : "Never",
                token.ExpiresAtUtc.HasValue ? token.ExpiresAtUtc.Value.ToLocalTime().ToString("dd MMM yyyy") : "Never",
                GetTokenStatus(token),
                token.IsRevoked ? "Revoked" : "Revoke");
        }

        _tokensGrid.Visible = tokens.Count > 0;
        _tokensEmptyState.Visible = tokens.Count == 0;
    }

    private async Task SaveCoreAsync(CancellationToken cancellationToken)
    {
        var currentUser = _session.CurrentUserContext.CurrentUser;
        if (currentUser is null)
        {
            ErrorDialogService.Show("No logged-in user was found.");
            return;
        }

        try
        {
            _saveButton.Enabled = false;
            var updatedUser = await _session.RunSerializedAsync(
                () => _session.UserCommands.UpdateEmailNotificationsPreferenceAsync(currentUser.Id, _emailNotifications.Checked, cancellationToken),
                cancellationToken);
            if (updatedUser is null)
            {
                ErrorDialogService.Show("Unable to update email notification preferences.");
                return;
            }

            _emailNotifications.Checked = updatedUser.EmailNotificationsEnabled;
            _status.Text = updatedUser.EmailNotificationsEnabled
                ? "Email notifications are enabled for your account."
                : "Email notifications are disabled for your account.";
            _status.ForeColor = JiraTheme.Green700;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!IsDisposed)
            {
                ErrorDialogService.Show(exception);
            }
        }
        finally
        {
            if (!IsDisposed)
            {
                _saveButton.Enabled = true;
            }
        }
    }

    private async Task CreateTokenCoreAsync(CancellationToken cancellationToken)
    {
        var currentUser = _session.CurrentUserContext.CurrentUser;
        if (currentUser is null)
        {
            ErrorDialogService.Show("No logged-in user was found.");
            return;
        }

        using var dialog = new CreateApiTokenDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _createTokenButton.Enabled = false;
            var result = await _session.RunSerializedAsync(
                () => _session.ApiTokens.CreateAsync(currentUser.Id, dialog.TokenName, dialog.ExpiresAtUtc, dialog.SelectedScopes, cancellationToken),
                cancellationToken);
            await LoadProfileCoreAsync(cancellationToken);
            using var generatedDialog = new GeneratedApiTokenDialog(result.RawToken);
            generatedDialog.ShowDialog(this);
            _status.Text = "API token created successfully.";
            _status.ForeColor = JiraTheme.Green700;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!IsDisposed)
            {
                ErrorDialogService.Show(exception);
            }
        }
        finally
        {
            if (!IsDisposed)
            {
                _createTokenButton.Enabled = true;
            }
        }
    }

    private async Task HandleTokenGridCellContentClickCoreAsync(DataGridViewCellEventArgs eventArgs, CancellationToken cancellationToken)
    {
        if (eventArgs.RowIndex < 0 || eventArgs.ColumnIndex != _tokensGrid.Columns.Count - 1 || eventArgs.RowIndex >= _tokens.Count)
        {
            return;
        }

        var token = _tokens[eventArgs.RowIndex];
        if (token.IsRevoked)
        {
            return;
        }

        if (_session.CurrentUserContext.CurrentUser is not { Id: var currentUserId })
        {
            ErrorDialogService.Show("No logged-in user was found.");
            return;
        }

        if (MessageBox.Show(this, $"Revoke API token '{token.Name}'? This action cannot be undone.", "Revoke API Token", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            await _session.RunSerializedAsync(
                () => _session.ApiTokens.RevokeAsync(token.Id, currentUserId, cancellationToken),
                cancellationToken);
            await LoadProfileCoreAsync(cancellationToken);
            _status.Text = "API token revoked.";
            _status.ForeColor = JiraTheme.Green700;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!IsDisposed)
            {
                ErrorDialogService.Show(exception);
            }
        }
    }

    private static string GetTokenStatus(ApiToken token)
    {
        if (token.IsRevoked)
        {
            return "Revoked";
        }

        if (token.ExpiresAtUtc.HasValue && token.ExpiresAtUtc.Value <= DateTime.UtcNow)
        {
            return "Expired";
        }

        return "Active";
    }
}
