using JiraClone.Application.Integrations;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Forms.Integrations;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public sealed class IntegrationSettingsControl : UserControl
{
    private readonly AppSession _session;
    private readonly FlowLayoutPanel _cards = new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        Padding = new Padding(24),
        BackColor = JiraTheme.BgSurface,
    };

    private readonly Dictionary<string, IntegrationCard> _cardByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _disposeCts = new();
    private CancellationTokenSource? _refreshCts;
    private Project? _project;

    public IntegrationSettingsControl(AppSession session)
    {
        _session = session;
        Dock = DockStyle.Fill;
        BackColor = JiraTheme.BgSurface;

        Controls.Add(_cards);
        AddCard(IntegrationNames.GitHub, "GitHub", "Link commits and pull requests to issue activity.");
        AddCard(IntegrationNames.Confluence, "Confluence", "Create and link knowledge-base pages from issues.");
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default) => LoadAsync(RestartRefreshCancellation(cancellationToken));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelPendingRefresh();
            _disposeCts.Cancel();
            foreach (var card in _cardByName.Values)
            {
                card.ConfigureRequested -= OnConfigureRequested;
                card.DisconnectRequested -= OnDisconnectRequested;
            }

            _disposeCts.Dispose();
        }

        base.Dispose(disposing);
    }

    private CancellationToken RestartRefreshCancellation(CancellationToken cancellationToken = default)
    {
        CancelPendingRefresh();
        _refreshCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, cancellationToken);
        return _refreshCts.Token;
    }

    private CancellationTokenSource CreateOperationSource(CancellationToken cancellationToken = default) =>
        CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, cancellationToken);

    private void CancelPendingRefresh()
    {
        if (_refreshCts is null)
        {
            return;
        }

        try
        {
            _refreshCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _refreshCts.Dispose();
        _refreshCts = null;
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<IntegrationStatus> statuses = [];
            await _session.RunSerializedAsync(async () =>
            {
                _project = await _session.Projects.GetActiveProjectAsync(cancellationToken);
                statuses = _project is null
                    ? []
                    : await _session.Integrations.GetProjectStatusesAsync(_project.Id, cancellationToken);
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var statusByName = statuses.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
            var currentMembership = GetCurrentMembership();
            var canManage = currentMembership?.ProjectRole is ProjectRole.Admin or ProjectRole.ProjectManager;

            foreach (var card in _cardByName.Values)
            {
                if (!statusByName.TryGetValue(card.IntegrationName, out var status))
                {
                    card.ApplyStatus(new IntegrationStatus(card.IntegrationName, card.DescriptionText, false, false, "Disconnected", null, "Not available."), canManage);
                    continue;
                }

                card.ApplyStatus(status, canManage);
            }
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

    private void AddCard(string integrationName, string title, string description)
    {
        var card = new IntegrationCard(title, description)
        {
            IntegrationName = integrationName,
            Width = 760,
            Margin = new Padding(0, 0, 0, 18),
        };
        card.ConfigureRequested += OnConfigureRequested;
        card.DisconnectRequested += OnDisconnectRequested;
        _cardByName[integrationName] = card;
        _cards.Controls.Add(card);
    }

    private async void OnConfigureRequested(object? sender, EventArgs e)
    {
        using var operationSource = CreateOperationSource();
        try
        {
            await HandleConfigureRequestedAsync(sender, operationSource.Token);
        }
        catch (OperationCanceledException) when (operationSource.IsCancellationRequested)
        {
        }
    }

    private async Task HandleConfigureRequestedAsync(object? sender, CancellationToken cancellationToken)
    {
        if (_project is null || sender is not IntegrationCard card)
        {
            return;
        }

        try
        {
            switch (card.IntegrationName)
            {
                case IntegrationNames.GitHub:
                {
                    var config = await _session.RunSerializedAsync(() => _session.Integrations.GetGitHubConfigAsync(_project.Id, cancellationToken), cancellationToken);
                    using var dialog = new GitHubIntegrationConfigDialog(config, card.CurrentStatus?.IsEnabled ?? true);
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    await _session.RunSerializedAsync(() => _session.Integrations.SaveGitHubConfigAsync(_project.Id, dialog.Config, dialog.IsEnabled, cancellationToken), cancellationToken);
                    break;
                }
                case IntegrationNames.Confluence:
                {
                    var config = await _session.RunSerializedAsync(() => _session.Integrations.GetConfluenceConfigAsync(_project.Id, cancellationToken), cancellationToken);
                    using var dialog = new ConfluenceIntegrationConfigDialog(config, card.CurrentStatus?.IsEnabled ?? true);
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    await _session.RunSerializedAsync(() => _session.Integrations.SaveConfluenceConfigAsync(_project.Id, dialog.Config, dialog.IsEnabled, cancellationToken), cancellationToken);
                    break;
                }
            }

            await RefreshAsync(cancellationToken);
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

    private async void OnDisconnectRequested(object? sender, EventArgs e)
    {
        using var operationSource = CreateOperationSource();
        try
        {
            await HandleDisconnectRequestedAsync(sender, operationSource.Token);
        }
        catch (OperationCanceledException) when (operationSource.IsCancellationRequested)
        {
        }
    }

    private async Task HandleDisconnectRequestedAsync(object? sender, CancellationToken cancellationToken)
    {
        if (_project is null || sender is not IntegrationCard card)
        {
            return;
        }

        if (MessageBox.Show(this, $"Disconnect {card.TitleText} from this project?", "Disconnect Integration", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            switch (card.IntegrationName)
            {
                case IntegrationNames.GitHub:
                    await _session.RunSerializedAsync(() => _session.Integrations.DisconnectGitHubAsync(_project.Id, cancellationToken), cancellationToken);
                    break;
                case IntegrationNames.Confluence:
                    await _session.RunSerializedAsync(() => _session.Integrations.DisconnectConfluenceAsync(_project.Id, cancellationToken), cancellationToken);
                    break;
            }

            await RefreshAsync(cancellationToken);
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

    private ProjectMember? GetCurrentMembership()
    {
        var currentUserId = _session.CurrentUserContext.CurrentUser?.Id;
        if (_project is null || !currentUserId.HasValue)
        {
            return null;
        }

        return _project.Members.FirstOrDefault(member => member.UserId == currentUserId.Value);
    }

    private sealed class IntegrationCard : Panel
    {
        private readonly Label _title = JiraControlFactory.CreateLabel(string.Empty);
        private readonly Label _description = JiraControlFactory.CreateLabel(string.Empty, true);
        private readonly Label _badge = JiraControlFactory.CreateLabel(string.Empty, true);
        private readonly Label _detail = JiraControlFactory.CreateLabel(string.Empty, true);
        private readonly Button _configure = JiraControlFactory.CreatePrimaryButton("Configure");
        private readonly Button _disconnect = JiraControlFactory.CreateSecondaryButton("Disconnect");
        private readonly Label _logo = JiraControlFactory.CreateLabel(string.Empty, true);

        public IntegrationCard(string title, string description)
        {
            TitleText = title;
            DescriptionText = description;
            Height = 154;
            BackColor = JiraTheme.BgSurface;
            Padding = new Padding(20);
            Margin = new Padding(0);

            _logo.Text = title[..1].ToUpperInvariant();
            _logo.TextAlign = ContentAlignment.MiddleCenter;
            _logo.Size = new Size(40, 40);
            _logo.BackColor = title == IntegrationNames.GitHub ? JiraTheme.Neutral500 : JiraTheme.Blue100;
            _logo.ForeColor = title == IntegrationNames.GitHub ? Color.White : JiraTheme.PrimaryActive;
            _logo.Location = new Point(20, 20);

            _title.Text = title;
            _title.Font = JiraTheme.FontH2;
            _title.Location = new Point(74, 18);
            _title.AutoSize = true;

            _badge.AutoSize = true;
            _badge.Padding = new Padding(10, 4, 10, 4);
            _badge.Location = new Point(74, 48);

            _description.Text = description;
            _description.MaximumSize = new Size(420, 0);
            _description.Location = new Point(74, 78);
            _description.AutoSize = true;

            _detail.AutoSize = true;
            _detail.ForeColor = JiraTheme.TextSecondary;
            _detail.Location = new Point(74, 112);

            _configure.AutoSize = false;
            _configure.Size = new Size(110, 38);
            _configure.Location = new Point(Width - 250, 58);
            _configure.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _configure.Click += OnConfigureClick;

            _disconnect.AutoSize = false;
            _disconnect.Size = new Size(110, 38);
            _disconnect.Location = new Point(Width - 128, 58);
            _disconnect.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _disconnect.Click += OnDisconnectClick;

            Controls.Add(_logo);
            Controls.Add(_title);
            Controls.Add(_badge);
            Controls.Add(_description);
            Controls.Add(_detail);
            Controls.Add(_configure);
            Controls.Add(_disconnect);
        }

        public string IntegrationName { get; init; } = string.Empty;
        public string TitleText { get; }
        public string DescriptionText { get; }
        public IntegrationStatus? CurrentStatus { get; private set; }
        public event EventHandler? ConfigureRequested;
        public event EventHandler? DisconnectRequested;

        public void ApplyStatus(IntegrationStatus status, bool canManage)
        {
            CurrentStatus = status;
            _badge.Text = status.BadgeText;
            _badge.BackColor = status.IsConfigured && status.IsEnabled ? Color.FromArgb(227, 252, 239) : JiraTheme.Neutral100;
            _badge.ForeColor = status.IsConfigured && status.IsEnabled ? JiraTheme.Green700 : JiraTheme.TextSecondary;
            _detail.Text = status.LastSyncAtUtc.HasValue
                ? $"{status.Detail}  Last sync: {status.LastSyncAtUtc.Value.ToLocalTime():dd MMM yyyy HH:mm}"
                : status.Detail ?? "Not configured.";
            _configure.Enabled = canManage;
            _disconnect.Enabled = canManage && status.IsConfigured;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _configure.Click -= OnConfigureClick;
                _disconnect.Click -= OnDisconnectClick;
            }

            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        private void OnConfigureClick(object? sender, EventArgs e) => ConfigureRequested?.Invoke(this, EventArgs.Empty);

        private void OnDisconnectClick(object? sender, EventArgs e) => DisconnectRequested?.Invoke(this, EventArgs.Empty);
    }
}


