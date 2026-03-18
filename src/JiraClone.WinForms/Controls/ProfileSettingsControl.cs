using JiraClone.WinForms.Composition;
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
    private readonly Label _status = JiraControlFactory.CreateLabel(string.Empty, true);
    private bool _isLoading;

    public ProfileSettingsControl(AppSession session)
    {
        _session = session;
        Dock = DockStyle.Fill;
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;

        _saveButton.AutoSize = false;
        _saveButton.Size = new Size(156, 36);
        _saveButton.Click += async (_, _) => await SaveAsync();

        _status.AutoSize = true;
        _status.ForeColor = JiraTheme.TextSecondary;
        _status.Margin = new Padding(0, 6, 0, 0);

        Controls.Add(BuildLayout());
        Load += async (_, _) => await RefreshProfileAsync();
    }

    public Task RefreshProfileAsync(CancellationToken cancellationToken = default) => LoadProfileAsync(cancellationToken);

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
        description.MaximumSize = new Size(720, 0);
        description.AutoSize = true;
        description.Margin = new Padding(0, 0, 0, 10);

        var hint = JiraControlFactory.CreateLabel(
            "When enabled, in-app notifications will also send email if SMTP is configured and your account has an email address.",
            true);
        hint.MaximumSize = new Size(720, 0);
        hint.AutoSize = true;
        hint.Margin = new Padding(0, 0, 0, 6);

        var actionRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 12, 0, 0),
            BackColor = JiraTheme.BgSurface,
        };
        actionRow.Controls.Add(_saveButton);

        layout.Controls.Add(title);
        layout.Controls.Add(description);
        layout.Controls.Add(hint);
        layout.Controls.Add(_emailNotifications);
        layout.Controls.Add(actionRow);
        layout.Controls.Add(_status);
        return layout;
    }

    private Task LoadProfileAsync(CancellationToken cancellationToken)
    {
        if (_isLoading)
        {
            return Task.CompletedTask;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _isLoading = true;
            var currentUser = _session.CurrentUserContext.CurrentUser;
            _emailNotifications.Enabled = currentUser is not null;
            _saveButton.Enabled = currentUser is not null;
            _emailNotifications.Checked = currentUser?.EmailNotificationsEnabled ?? false;
            _status.Text = currentUser is null ? "Log in to manage profile preferences." : string.Empty;
            _status.ForeColor = JiraTheme.TextSecondary;
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
        }

        return Task.CompletedTask;
    }

    private async Task SaveAsync()
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
            var updatedUser = await _session.UserCommands.UpdateEmailNotificationsPreferenceAsync(currentUser.Id, _emailNotifications.Checked);
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
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
        finally
        {
            _saveButton.Enabled = true;
        }
    }
}