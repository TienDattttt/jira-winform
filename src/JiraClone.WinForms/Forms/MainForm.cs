using System.Drawing.Drawing2D;
using System.Drawing.Text;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.Infrastructure.Session;
using JiraClone.Application.Roles;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Controls;
using JiraClone.WinForms.Helpers;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;
using Microsoft.Extensions.Logging;

namespace JiraClone.WinForms.Forms;

public class MainForm : Form
{
    private readonly AppSession _session;
    private readonly ISessionPersistenceService _sessionPersistence;
    private readonly string _displayName;
    private readonly Panel _contentPanel = new() { Dock = DockStyle.Fill, BackColor = JiraTheme.BgPage };
    private readonly Label _breadcrumbLabel = new() { AutoSize = true, Font = JiraTheme.FontSmall, ForeColor = JiraTheme.TextSecondary };
    private readonly TextBox _searchBox = JiraControlFactory.CreateTextBox();
    private readonly SidebarNavItem _projectsItem = new(NavKind.Projects, "Projects");
    private readonly SidebarNavItem _dashboardItem = new(NavKind.Dashboard, "Dashboard");
    private readonly SidebarNavItem _boardItem = new(NavKind.Board, "Board");
    private readonly SidebarNavItem _backlogItem = new(NavKind.Backlog, "Backlog");
    private readonly SidebarNavItem _roadmapItem = new(NavKind.Roadmap, "Roadmap");
    private readonly SidebarNavItem _sprintsItem = new(NavKind.Sprints, "Sprints");
    private readonly SidebarNavItem _issuesItem = new(NavKind.Issues, "Issues");
    private readonly SidebarNavItem _reportsItem = new(NavKind.Reports, "Reports");
    private readonly SidebarNavItem _usersItem = new(NavKind.Users, "Users");
    private readonly SidebarNavItem _settingsItem = new(NavKind.Settings, "Settings");
    private readonly ProjectSwitcherControl _projectSwitcher;
    private readonly InitialsAvatar _sidebarAvatar;
    private readonly InitialsAvatar _navbarAvatar;
    private readonly Label _sidebarUserLabel;
    private readonly Button _logoutButton;
    private readonly Button _createIssueButton;
    private readonly Button _cancelButton;
    private readonly Button _notificationButton;
    private readonly Label _notificationBadge = new() { AutoSize = false, Size = new Size(18, 18), TextAlign = ContentAlignment.MiddleCenter, Font = JiraTheme.FontCaption, BackColor = JiraTheme.Danger, ForeColor = Color.White, Visible = false };
    private readonly BorderPanel _notificationDropdown = new() { Size = new Size(320, 420), Visible = false, BackColor = JiraTheme.BgSurface, Padding = new Padding(0) };
    private readonly FlowLayoutPanel _notificationList = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = JiraTheme.BgSurface, Padding = new Padding(0) };
    private readonly Label _notificationEmpty = JiraControlFactory.CreateLabel("No notifications yet.", true);
    private readonly Button _markAllReadButton = JiraControlFactory.CreateSecondaryButton("Mark all as read");

    private string _projectName = "Project";
    private SidebarNavItem? _activeNavItem;
    private Control? _activeContent;
    private CancellationTokenSource? _uiOperationCts;
    private System.Threading.Timer? _notificationTimer;
    private int _notificationPollInFlight;
    private bool _isUiBusy;
    private Panel? _navbarRightPanel;
    private readonly ILogger<MainForm> _logger;

    public MainForm(AppSession session, string displayName, ISessionPersistenceService sessionPersistence)
    {
        _session = session;
        _sessionPersistence = sessionPersistence;
        _logger = session.CreateLogger<MainForm>();
        _displayName = session.CurrentUserContext.CurrentUser?.DisplayName ?? displayName;
        _projectSwitcher = new ProjectSwitcherControl(session);

        Text = $"Jira Clone Desktop - {_displayName}";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 760);
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;

        _searchBox.Width = 340;
        _searchBox.MinimumSize = new Size(260, 40);
        _searchBox.PlaceholderText = "Search projects";
        _searchBox.TextChanged += OnSearchBoxTextChanged;

        var initials = BuildInitials(_displayName);
        _sidebarAvatar = new InitialsAvatar(initials, 32) { BackCircleColor = JiraTheme.Blue600 };
        _navbarAvatar = new InitialsAvatar(initials, 32) { BackCircleColor = JiraTheme.Blue600 };
        _sidebarUserLabel = JiraControlFactory.CreateLabel(_displayName);
        _sidebarUserLabel.ForeColor = Color.FromArgb(220, 255, 255, 255);
        _sidebarUserLabel.Font = JiraTheme.FontSmall;
        _sidebarUserLabel.AutoEllipsis = true;
        _sidebarUserLabel.MaximumSize = new Size(150, 30);
        _logoutButton = JiraControlFactory.CreateSecondaryButton("Logout");
        _logoutButton.AutoSize = false;
        _logoutButton.Width = 124;
        _logoutButton.Height = 40;
        _logoutButton.MinimumSize = new Size(124, 36);
        _logoutButton.Click += OnLogoutButtonClick;
        _createIssueButton = JiraControlFactory.CreatePrimaryButton("Create");
        _createIssueButton.AutoSize = false;
        _createIssueButton.Size = new Size(120, 40);
        _createIssueButton.Click += OnCreateIssueButtonClick;
        _cancelButton = JiraControlFactory.CreateSecondaryButton("Cancel");
        _cancelButton.AutoSize = false;
        _cancelButton.Size = new Size(108, 40);
        _cancelButton.Visible = false;
        _cancelButton.Enabled = false;
        _cancelButton.Click += OnCancelButtonClick;
        _notificationButton = JiraControlFactory.CreateSecondaryButton(string.Empty);
        _notificationButton.AutoSize = false;
        _notificationButton.Size = new Size(40, 38);
        _notificationButton.Image = JiraIcons.GetBellIcon(JiraTheme.TextPrimary, 16);
        _notificationButton.ImageAlign = ContentAlignment.MiddleCenter;
        _notificationButton.Padding = new Padding(0);
        _notificationButton.Click += OnNotificationButtonClick;
        ConfigureNotificationDropdown();

        BuildLayout();
        WireNavigation();
        _session.ProjectChanged += HandleSessionProjectChanged;

        Shown += OnMainFormShown;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelActiveUiOperation();
            _uiOperationCts?.Dispose();
            _session.ProjectChanged -= HandleSessionProjectChanged;
            _searchBox.TextChanged -= OnSearchBoxTextChanged;
            _logoutButton.Click -= OnLogoutButtonClick;
            _createIssueButton.Click -= OnCreateIssueButtonClick;
            _cancelButton.Click -= OnCancelButtonClick;
            _notificationButton.Click -= OnNotificationButtonClick;
            _markAllReadButton.Click -= OnMarkAllReadButtonClick;
            if (_navbarRightPanel is not null)
            {
                _navbarRightPanel.Resize -= OnNavbarRightPanelResize;
            }
            _notificationTimer?.Dispose();
            Shown -= OnMainFormShown;
            _projectsItem.Click -= OnProjectsItemClick;
            _dashboardItem.Click -= OnDashboardItemClick;
            _boardItem.Click -= OnBoardItemClick;
            _backlogItem.Click -= OnBacklogItemClick;
            _roadmapItem.Click -= OnRoadmapItemClick;
            _sprintsItem.Click -= OnSprintsItemClick;
            _issuesItem.Click -= OnIssuesItemClick;
            _reportsItem.Click -= OnReportsItemClick;
            _usersItem.Click -= OnUsersItemClick;
            _settingsItem.Click -= OnSettingsItemClick;
            if (_activeContent is ProjectListForm projectListForm)
            {
                projectListForm.ProjectOpened -= HandleProjectListOpened;
            }
        }

        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = JiraTheme.BgPage,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, JiraTheme.SidebarWidth));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildSidebar(), 0, 0);
        root.Controls.Add(BuildMainArea(), 1, 0);
        Controls.Add(root);
        Controls.Add(_notificationDropdown);
        _notificationDropdown.BringToFront();
        PositionNotificationDropdown();
    }

    private Control BuildSidebar()
    {
        var sidebar = new DoubleBufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSidebar,
            Padding = new Padding(0, 20, 0, 20),
        };

        var brandRow = new Panel
        {
            Dock = DockStyle.Top,
            Height = 62,
            Padding = new Padding(20, 8, 20, 8),
            BackColor = JiraTheme.BgSidebar,
        };

        var brandAvatar = new InitialsAvatar("J", 34) { BackCircleColor = JiraTheme.Blue600 };
        var brandLabel = JiraControlFactory.CreateLabel("Jira Clone");
        brandLabel.ForeColor = Color.White;
        brandLabel.Font = JiraTheme.FontH2;
        brandLabel.Location = new Point(52, 8);
        brandRow.Controls.Add(brandAvatar);
        brandRow.Controls.Add(brandLabel);
        brandAvatar.Location = new Point(20, 7);

        var separatorTop = JiraControlFactory.CreateSeparator();
        separatorTop.Dock = DockStyle.Top;
        separatorTop.BackColor = Color.FromArgb(28, 255, 255, 255);

        var navStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(0, 14, 0, 0),
            BackColor = JiraTheme.BgSidebar,
        };
        var navItems = new List<Control> { _projectsItem, _dashboardItem, _boardItem, _backlogItem, _roadmapItem, _sprintsItem, _issuesItem, _reportsItem };
        if (_session.Authorization.IsInRole(RoleCatalog.Admin))
        {
            navItems.Add(_usersItem);
        }

        navItems.Add(_settingsItem);
        navStack.Controls.AddRange(navItems.ToArray());

        var bottomSection = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 140,
            Padding = new Padding(20, 16, 20, 0),
            BackColor = JiraTheme.BgSidebar,
        };

        var bottomSeparator = JiraControlFactory.CreateSeparator();
        bottomSeparator.Dock = DockStyle.Top;
        bottomSeparator.BackColor = Color.FromArgb(28, 255, 255, 255);

        var userRow = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = JiraTheme.BgSidebar };
        userRow.Controls.Add(_sidebarAvatar);
        userRow.Controls.Add(_sidebarUserLabel);
        _sidebarAvatar.Location = new Point(0, 2);
        _sidebarUserLabel.Location = new Point(44, 8);

        var logoutHost = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = JiraTheme.BgSidebar };
        logoutHost.Controls.Add(_logoutButton);
        _logoutButton.Location = new Point(0, 6);

        bottomSection.Controls.Add(logoutHost);
        bottomSection.Controls.Add(userRow);
        bottomSection.Controls.Add(bottomSeparator);

        sidebar.Controls.Add(bottomSection);
        sidebar.Controls.Add(navStack);
        sidebar.Controls.Add(_projectSwitcher);
        sidebar.Controls.Add(separatorTop);
        sidebar.Controls.Add(brandRow);
        return sidebar;
    }

    private Control BuildMainArea()
    {
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = JiraTheme.BgPage,
        };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, JiraTheme.NavbarHeight));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.Controls.Add(BuildNavbar(), 0, 0);
        main.Controls.Add(_contentPanel, 0, 1);
        return main;
    }

    private Control BuildNavbar()
    {
        var navbar = new BottomBorderPanel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(20, 10, 20, 10),
        };

        _navbarRightPanel = new Panel { Dock = DockStyle.Right, Width = 800, BackColor = JiraTheme.BgSurface };
        _navbarRightPanel.Controls.Add(_notificationBadge);
        _navbarRightPanel.Controls.Add(_notificationButton);
        _navbarRightPanel.Controls.Add(_navbarAvatar);
        _navbarRightPanel.Controls.Add(_searchBox);
        _navbarRightPanel.Controls.Add(_createIssueButton);
        _navbarRightPanel.Controls.Add(_cancelButton);
        _navbarRightPanel.Resize += OnNavbarRightPanelResize;
        LayoutNavbarRightPanel();

        var leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
        leftPanel.Controls.Add(_breadcrumbLabel);
        _breadcrumbLabel.Location = new Point(0, 10);

        navbar.Controls.Add(_navbarRightPanel);
        navbar.Controls.Add(leftPanel);
        return navbar;
    }

    private void ConfigureNotificationDropdown()
    {
        _notificationDropdown.Visible = false;
        _notificationDropdown.Padding = new Padding(0);
        _notificationDropdown.BorderStyle = BorderStyle.FixedSingle;
        _notificationDropdown.BackColor = JiraTheme.BgSurface;

        var header = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = JiraTheme.BgSurface, Padding = new Padding(12, 8, 12, 8) };
        var title = JiraControlFactory.CreateLabel("Notifications");
        title.Dock = DockStyle.Left;
        title.Width = 140;
        title.Font = JiraTheme.FontSmall;
        _markAllReadButton.Dock = DockStyle.Right;
        _markAllReadButton.AutoSize = false;
        _markAllReadButton.Size = new Size(124, 30);
        _markAllReadButton.Click += OnMarkAllReadButtonClick;
        header.Controls.Add(_markAllReadButton);
        header.Controls.Add(title);

        _notificationList.Padding = new Padding(0, 0, 0, 8);
        _notificationEmpty.Dock = DockStyle.Fill;
        _notificationEmpty.TextAlign = ContentAlignment.MiddleCenter;
        _notificationEmpty.Visible = false;

        _notificationDropdown.Controls.Add(_notificationEmpty);
        _notificationDropdown.Controls.Add(_notificationList);
        _notificationDropdown.Controls.Add(header);
    }

    private void PositionNotificationDropdown()
    {
        if (!_notificationDropdown.IsHandleCreated && !IsHandleCreated)
        {
            return;
        }

        _notificationDropdown.Location = new Point(Math.Max(16, ClientSize.Width - _notificationDropdown.Width - 28), JiraTheme.NavbarHeight + 6);
        _notificationDropdown.BringToFront();
    }

    private void StartNotificationWorker()
    {
        _notificationTimer ??= new System.Threading.Timer(
            async _ => await PollNotificationsAsync(),
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(30));
    }

    private async Task PollNotificationsAsync()
    {
        if (IsDisposed || _session.CurrentUserContext.CurrentUser is null)
        {
            return;
        }

        if (Interlocked.Exchange(ref _notificationPollInFlight, 1) == 1)
        {
            return;
        }

        try
        {
            var userId = _session.CurrentUserContext.RequireUserId();
            var unreadCount = await _session.Notifications.GetUnreadCountAsync(userId);
            var notifications = await _session.Notifications.GetRecentAsync(userId, 20);
            if (!IsDisposed && IsHandleCreated)
            {
                BeginInvoke((MethodInvoker)(() => BindNotifications(notifications, unreadCount)));
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Notification poll failed.");
        }
        finally
        {
            Interlocked.Exchange(ref _notificationPollInFlight, 0);
        }
    }

    private void BindNotifications(IReadOnlyList<NotificationItemDto> notifications, int unreadCount)
    {
        _notificationBadge.Text = unreadCount > 99 ? "99+" : unreadCount.ToString();
        _notificationBadge.Visible = unreadCount > 0;
        _notificationList.SuspendLayout();
        try
        {
            foreach (Control control in _notificationList.Controls)
            {
                if (control is NotificationRowControl row)
                {
                    row.NotificationRequested -= OnNotificationRowRequested;
                }

                control.Dispose();
            }

            _notificationList.Controls.Clear();
            foreach (var notification in notifications)
            {
                _notificationList.Controls.Add(CreateNotificationRow(notification));
            }
        }
        finally
        {
            _notificationList.ResumeLayout();
        }

        _notificationEmpty.Visible = notifications.Count == 0;
        _notificationList.Visible = notifications.Count > 0;
        _markAllReadButton.Enabled = unreadCount > 0;
    }

    private Control CreateNotificationRow(NotificationItemDto notification)
    {
        var row = new NotificationRowControl(notification, _notificationDropdown.Width - 24);
        row.NotificationRequested += OnNotificationRowRequested;
        return row;
    }

    private async Task OpenNotificationAsync(NotificationItemDto notification)
    {
        try
        {
            var userId = _session.CurrentUserContext.RequireUserId();
            await _session.Notifications.MarkReadAsync(notification.Id, userId);
            if (notification.ProjectId.HasValue && _session.ActiveProject?.Id != notification.ProjectId.Value)
            {
                await _session.SetActiveProjectAsync(notification.ProjectId.Value);
            }

            _notificationDropdown.Visible = false;
            await PollNotificationsAsync();

            if (notification.IssueId.HasValue)
            {
                var projectId = notification.ProjectId ?? _session.ActiveProject?.Id ?? 0;
                if (projectId > 0)
                {
                    using var dialog = new IssueDetailsForm(_session, notification.IssueId.Value, projectId);
                    if (dialog.ShowDialog(this) == DialogResult.OK && _activeContent is not null)
                    {
                        await RefreshActiveContentAsync();
                    }
                }
            }
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task MarkAllNotificationsReadAsync()
    {
        try
        {
            var userId = _session.CurrentUserContext.RequireUserId();
            await _session.Notifications.MarkAllReadAsync(userId);
            await PollNotificationsAsync();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private void LayoutNavbarRightPanel()
    {
        if (_navbarRightPanel is null)
        {
            return;
        }

        _navbarAvatar.Location = new Point(_navbarRightPanel.Width - _navbarAvatar.Width, 1);
        _notificationButton.Location = new Point(_navbarAvatar.Left - _notificationButton.Width - 12, 0);
        _notificationBadge.Location = new Point(_notificationButton.Right - 10, -2);
        _searchBox.Location = new Point(_notificationButton.Left - _searchBox.Width - 16, 0);
        _createIssueButton.Location = new Point(_searchBox.Left - _createIssueButton.Width - 12, 0);
        _cancelButton.Location = new Point(_createIssueButton.Left - _cancelButton.Width - 12, 0);
    }

    private void OnNavbarRightPanelResize(object? sender, EventArgs e)
    {
        LayoutNavbarRightPanel();
    }

    private async void OnMarkAllReadButtonClick(object? sender, EventArgs e)
    {
        await MarkAllNotificationsReadAsync();
    }

    private async void OnNotificationRowRequested(object? sender, NotificationItemDto notification)
    {
        await OpenNotificationAsync(notification);
    }

    private void OnNotificationButtonClick(object? sender, EventArgs e)
    {
        _notificationDropdown.Visible = !_notificationDropdown.Visible;
        if (_notificationDropdown.Visible)
        {
            PositionNotificationDropdown();
            _notificationDropdown.BringToFront();
            _ = PollNotificationsAsync();
        }
    }

    private static string FormatRelativeTime(DateTime utc)
    {
        var elapsed = DateTime.UtcNow - utc;
        if (elapsed.TotalMinutes < 1)
        {
            return "now";
        }

        if (elapsed.TotalHours < 1)
        {
            return $"{Math.Max(1, (int)elapsed.TotalMinutes)}m";
        }

        if (elapsed.TotalDays < 1)
        {
            return $"{Math.Max(1, (int)elapsed.TotalHours)}h";
        }

        return $"{Math.Max(1, (int)elapsed.TotalDays)}d";
    }

    private static string GetNotificationGlyph(NotificationType type) => type switch
    {
        NotificationType.IssueAssigned => "A",
        NotificationType.IssueStatusChanged => "S",
        NotificationType.CommentAdded => "C",
        NotificationType.CommentMentioned => "@",
        NotificationType.SprintStarted => "S",
        NotificationType.SprintCompleted => "F",
        _ => "N"
    };

    private static Color GetNotificationColor(NotificationType type) => type switch
    {
        NotificationType.IssueAssigned => JiraTheme.Primary,
        NotificationType.IssueStatusChanged => JiraTheme.Warning,
        NotificationType.CommentAdded => JiraTheme.Green500,
        NotificationType.CommentMentioned => JiraTheme.Purple500,
        NotificationType.SprintStarted => JiraTheme.Teal500,
        NotificationType.SprintCompleted => JiraTheme.Red600,
        _ => JiraTheme.Neutral500
    };
    private void WireNavigation()
    {
        _projectsItem.Click += OnProjectsItemClick;
        _dashboardItem.Click += OnDashboardItemClick;
        _boardItem.Click += OnBoardItemClick;
        _backlogItem.Click += OnBacklogItemClick;
        _roadmapItem.Click += OnRoadmapItemClick;
        _sprintsItem.Click += OnSprintsItemClick;
        _issuesItem.Click += OnIssuesItemClick;
        _reportsItem.Click += OnReportsItemClick;
        _usersItem.Click += OnUsersItemClick;
        _settingsItem.Click += OnSettingsItemClick;
    }

    private ProjectListForm CreateProjectListControl()
    {
        var control = new ProjectListForm(_session);
        control.ProjectOpened += HandleProjectListOpened;
        return control;
    }

    private void NavigateTo(SidebarNavItem navItem, Func<Control> createContent)
    {
        _activeNavItem?.SetActive(false);
        _activeNavItem = navItem;
        _activeNavItem.SetActive(true);

        if (_activeContent is ProjectListForm existingProjectList)
        {
            existingProjectList.ProjectOpened -= HandleProjectListOpened;
        }

        if (_activeContent is not null)
        {
            _contentPanel.Controls.Remove(_activeContent);
            _activeContent.Dispose();
        }

        _activeContent = createContent();
        _activeContent.Dock = DockStyle.Fill;
        _contentPanel.Controls.Add(_activeContent);
        UpdateProjectChrome();
        ApplyShellSearch();
    }

    private async Task LoadProjectContextAsync(CancellationToken cancellationToken = default)
    {
        await _session.InitializeActiveProjectAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        UpdateProjectChrome();

        if (_session.ActiveProject is null)
        {
            NavigateTo(_projectsItem, CreateProjectListControl);
            return;
        }

        NavigateTo(_boardItem, () => new BoardForm(_session, activeSprintOnly: true));
    }

    private async void HandleSessionProjectChanged(object? sender, AppSession.ProjectChangedEventArgs eventArgs)
    {
        if (IsDisposed)
        {
            return;
        }

        await RunCancelableUiOperationAsync(async cancellationToken =>
        {
            UpdateProjectChrome();
            if (_session.ActiveProject is null && _activeNavItem?.Kind != NavKind.Projects)
            {
                NavigateTo(_projectsItem, CreateProjectListControl);
                return;
            }

            if (_activeContent is not null)
            {
                await RefreshActiveContentAsync(cancellationToken);
            }
        });
    }

    private void UpdateProjectChrome()
    {
        _projectName = _session.ActiveProject?.Name ?? "Projects";
        _createIssueButton.Enabled = _session.ActiveProject is not null && !_isUiBusy;
        _searchBox.PlaceholderText = GetSearchPlaceholder();
        _breadcrumbLabel.Text = BuildBreadcrumb();
    }

    private string GetSearchPlaceholder()
    {
        return _activeNavItem?.Kind == NavKind.Projects
            ? "Search your projects"
            : _session.ActiveProject is not null
                ? $"Search in {_projectName}"
                : "Search projects";
    }

    private string BuildBreadcrumb()
    {
        if (_activeNavItem is null)
        {
            return _session.ActiveProject is null ? "Projects > Your Projects" : $"Projects > {_projectName}";
        }

        if (_activeNavItem.Kind == NavKind.Projects)
        {
            return "Projects > Your Projects";
        }

        return _session.ActiveProject is null
            ? $"Projects > {_activeNavItem.TextLabel}"
            : $"Projects > {_projectName} > {_activeNavItem.TextLabel}";
    }

    private async Task CreateIssueAsync()
    {
        try
        {
            var project = await _session.Projects.GetActiveProjectAsync();
            if (project is null)
            {
                ErrorDialogService.Show("No active project found.");
                return;
            }

            using var dialog = new IssueEditorForm(_session, project.Id, null);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                await RunCancelableUiOperationAsync(RefreshActiveContentAsync);
            }
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task RefreshActiveContentAsync(CancellationToken cancellationToken = default)
    {
        switch (_activeContent)
        {
            case DashboardForm dashboardForm:
                await dashboardForm.RefreshDashboardAsync(cancellationToken);
                break;
            case BoardForm boardForm:
                await boardForm.RefreshBoardAsync(cancellationToken);
                break;
            case RoadmapForm roadmapForm:
                await roadmapForm.RefreshRoadmapAsync(cancellationToken);
                break;
            case IssueNavigatorForm issueNavigator:
                await issueNavigator.RefreshIssuesAsync(cancellationToken);
                break;
            case SprintManagementForm sprintManagementForm:
                await sprintManagementForm.RefreshSprintsAsync(cancellationToken);
                break;
            case UserManagementForm userManagementForm:
                await userManagementForm.RefreshUsersAsync(cancellationToken);
                break;
            case ReportsForm reportsForm:
                await reportsForm.RefreshReportsAsync(cancellationToken);
                break;
            case ProjectSettingsForm projectSettingsForm:
                await projectSettingsForm.RefreshProjectAsync(cancellationToken);
                break;
            case ProjectListForm projectListForm:
                await projectListForm.RefreshProjectsAsync(cancellationToken);
                break;
        }
    }

    private void ApplyShellSearch()
    {
        switch (_activeContent)
        {
            case DashboardForm dashboardForm:
                dashboardForm.SetShellSearch(_searchBox.Text);
                break;
            case BoardForm boardForm:
                boardForm.SetShellSearch(_searchBox.Text);
                break;
            case RoadmapForm roadmapForm:
                roadmapForm.SetShellSearch(_searchBox.Text);
                break;
            case IssueNavigatorForm issueNavigator:
                issueNavigator.SetShellSearch(_searchBox.Text);
                break;
            case SprintManagementForm sprintManagementForm:
                sprintManagementForm.SetShellSearch(_searchBox.Text);
                break;
            case UserManagementForm userManagementForm:
                userManagementForm.SetShellSearch(_searchBox.Text);
                break;
            case ReportsForm reportsForm:
                reportsForm.SetShellSearch(_searchBox.Text);
                break;
            case ProjectSettingsForm projectSettingsForm:
                projectSettingsForm.SetShellSearch(_searchBox.Text);
                break;
            case ProjectListForm projectListForm:
                projectListForm.SetShellSearch(_searchBox.Text);
                break;
        }
    }
    private async void OnMainFormShown(object? sender, EventArgs e)
    {
        StartNotificationWorker();
        await RunCancelableUiOperationAsync(LoadProjectContextAsync);
    }

    private void OnSearchBoxTextChanged(object? sender, EventArgs e)
    {
        ApplyShellSearch();
    }

    private async void OnLogoutButtonClick(object? sender, EventArgs e)
    {
        await LogoutAsync();
    }

    private async void OnCreateIssueButtonClick(object? sender, EventArgs e)
    {
        await CreateIssueAsync();
    }

    private void OnCancelButtonClick(object? sender, EventArgs e)
    {
        CancelActiveUiOperation();
    }

    private void OnDashboardItemClick(object? sender, EventArgs e)
    {
        NavigateTo(_dashboardItem, () => new DashboardForm(_session));
    }

    private void OnProjectsItemClick(object? sender, EventArgs e)
    {
        NavigateTo(_projectsItem, CreateProjectListControl);
    }

    private void OnBoardItemClick(object? sender, EventArgs e)
    {
        NavigateTo(_boardItem, () => new BoardForm(_session, activeSprintOnly: true));
    }

    private void OnBacklogItemClick(object? sender, EventArgs e)
    {
        NavigateTo(_backlogItem, () => new BoardForm(_session, activeSprintOnly: false));
    }

    private void OnRoadmapItemClick(object? sender, EventArgs e)
    {
        NavigateTo(_roadmapItem, () => new RoadmapForm(_session));
    }

    private void OnSprintsItemClick(object? sender, EventArgs e)
    {
        NavigateTo(_sprintsItem, () => new SprintManagementForm(_session));
    }

    private void OnIssuesItemClick(object? sender, EventArgs e)
    {
        NavigateTo(_issuesItem, () => new IssueNavigatorForm(_session));
    }

    private void OnReportsItemClick(object? sender, EventArgs e)
    {
        NavigateTo(_reportsItem, () => new ReportsForm(_session));
    }

    private void OnSettingsItemClick(object? sender, EventArgs e)
    {
        NavigateTo(_settingsItem, () => new ProjectSettingsForm(_session));
    }

    private void HandleProjectListOpened(object? sender, EventArgs e)
    {
        NavigateTo(_boardItem, () => new BoardForm(_session, activeSprintOnly: true));
    }

    private async Task RunCancelableUiOperationAsync(Func<CancellationToken, Task> operation)
    {
        var operationCts = BeginUiOperation();
        try
        {
            await operation(operationCts.Token);
        }
        catch (OperationCanceledException) when (operationCts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
        finally
        {
            EndUiOperation(operationCts);
        }
    }

    private CancellationTokenSource BeginUiOperation()
    {
        CancelActiveUiOperation();
        _uiOperationCts?.Dispose();
        _uiOperationCts = new CancellationTokenSource();
        SetUiBusy(true);
        return _uiOperationCts;
    }

    private void EndUiOperation(CancellationTokenSource operationCts)
    {
        if (!ReferenceEquals(_uiOperationCts, operationCts))
        {
            operationCts.Dispose();
            return;
        }

        _uiOperationCts.Dispose();
        _uiOperationCts = null;
        SetUiBusy(false);
    }

    private void CancelActiveUiOperation()
    {
        if (_uiOperationCts is { IsCancellationRequested: false })
        {
            _uiOperationCts.Cancel();
        }
    }

    private void SetUiBusy(bool isBusy)
    {
        _isUiBusy = isBusy;
        _cancelButton.Visible = isBusy;
        _cancelButton.Enabled = isBusy;
        _cancelButton.BringToFront();
        UpdateProjectChrome();
    }

    private async Task LogoutAsync()
    {
        if (MessageBox.Show(this, "Log out from Jira Clone?", "Logout", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            if (_session.CurrentUserContext.CurrentUser is { Id: var userId })
            {
                await _session.Authentication.ClearPersistentSessionAsync(userId);
            }
            else
            {
                _session.CurrentUserContext.Clear();
            }

            await _sessionPersistence.ClearAsync();
            _session.CurrentUserContext.Clear();
            System.Windows.Forms.Application.Restart();
            Close();
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private static string BuildInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(2).Select(x => char.ToUpperInvariant(x[0])).ToArray();
        return parts.Length == 0 ? "U" : new string(parts);
    }

    private class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    private sealed class BorderPanel : DoubleBufferedPanel
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    private sealed class BottomBorderPanel : DoubleBufferedPanel
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }

    private sealed class BorderFlowLayoutPanel : FlowLayoutPanel
    {
        public BorderFlowLayoutPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    private sealed class NotificationRowControl : DoubleBufferedPanel
    {
        private readonly NotificationItemDto _notification;
        private bool _hovered;

        public NotificationRowControl(NotificationItemDto notification, int width)
        {
            _notification = notification;
            Width = width;
            Height = 68;
            Margin = new Padding(8, 8, 8, 0);
            Padding = new Padding(10, 10, 10, 10);
            Cursor = Cursors.Hand;
            BackColor = notification.IsRead ? JiraTheme.BgSurface : JiraTheme.Blue100;
        }

        public event EventHandler<NotificationItemDto>? NotificationRequested;

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            NotificationRequested?.Invoke(this, _notification);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var backgroundColor = _notification.IsRead ? JiraTheme.BgSurface : JiraTheme.Blue100;
            if (_hovered)
            {
                backgroundColor = JiraTheme.Neutral100;
            }

            using var background = new SolidBrush(backgroundColor);
            using var border = new Pen(JiraTheme.Border);
            using var glyphBrush = new SolidBrush(GetNotificationColor(_notification.Type));
            e.Graphics.FillRectangle(background, ClientRectangle);
            e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            e.Graphics.FillEllipse(glyphBrush, 10, 12, 24, 24);
            TextRenderer.DrawText(e.Graphics, GetNotificationGlyph(_notification.Type), JiraTheme.FontCaption, new Rectangle(10, 12, 24, 24), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics, _notification.Title, JiraTheme.FontSmall, new Rectangle(46, 4, 190, 20), JiraTheme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, FormatRelativeTime(_notification.CreatedAtUtc), JiraTheme.FontCaption, new Rectangle(240, 4, 48, 18), JiraTheme.TextSecondary, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, _notification.Body, JiraTheme.FontCaption, new Rectangle(46, 26, 242, 30), JiraTheme.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak);
        }
    }

    private void OnUsersItemClick(object? sender, EventArgs e)
    {
        if (!_session.Authorization.IsInRole(RoleCatalog.Admin))
        {
            return;
        }

        NavigateTo(_usersItem, () => new UserManagementForm(_session));
    }

    private enum NavKind
    {
        Projects,
        Dashboard,
        Board,
        Backlog,
        Roadmap,
        Sprints,
        Issues,
        Reports,
        Users,
        Settings
    }

    private sealed class SidebarNavItem : DoubleBufferedPanel
    {
        private readonly NavKind _kind;
        private bool _hovered;
        private bool _active;

        public SidebarNavItem(NavKind kind, string text)
        {
            _kind = kind;
            TextLabel = text;
            Height = 44;
            Width = JiraTheme.SidebarWidth;
            Cursor = Cursors.Hand;
            Margin = new Padding(0);
            BackColor = Color.Transparent;



        }

        public string TextLabel { get; }
        public NavKind Kind => _kind;

        public void SetActive(bool active)
        {
            _active = active;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SetClip(new Rectangle(0, 0, Width, Height));
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var background = _active ? JiraTheme.Blue600 : _hovered ? JiraTheme.SidebarHover : Color.Transparent;
            using var backgroundBrush = new SolidBrush(background);
            e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);

            if (_active)
            {
                using var activeBrush = new SolidBrush(Color.White);
                e.Graphics.FillRectangle(activeBrush, 0, 0, 3, Height);
            }

            DrawIcon(e.Graphics, new Rectangle(18, 8, 20, 28));
            TextRenderer.DrawText(e.Graphics, TextLabel, JiraTheme.FontSmall, new Rectangle(48, 0, Width - 64, Height), JiraTheme.SidebarText, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        private void DrawIcon(Graphics graphics, Rectangle bounds)
        {
            using var pen = new Pen(Color.White, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var fill = new SolidBrush(Color.White);
            var middleY = bounds.Top + bounds.Height / 2;

            switch (_kind)
            {
                case NavKind.Projects:
                    graphics.DrawRectangle(pen, bounds.X + 1, bounds.Y + 6, 15, 11);
                    graphics.DrawLine(pen, bounds.X + 4, bounds.Y + 6, bounds.X + 7, bounds.Y + 2);
                    graphics.DrawLine(pen, bounds.X + 7, bounds.Y + 2, bounds.X + 12, bounds.Y + 2);
                    graphics.DrawLine(pen, bounds.X + 12, bounds.Y + 2, bounds.X + 14, bounds.Y + 6);
                    break;
                case NavKind.Dashboard:
                    graphics.DrawRectangle(pen, bounds.X + 1, bounds.Y + 4, 15, 13);
                    graphics.DrawLine(pen, bounds.X + 1, bounds.Y + 9, bounds.X + 16, bounds.Y + 9);
                    graphics.DrawLine(pen, bounds.X + 7, bounds.Y + 4, bounds.X + 7, bounds.Y + 17);
                    graphics.FillEllipse(fill, bounds.X + 3, bounds.Y + 6, 2, 2);
                    graphics.FillEllipse(fill, bounds.X + 9, bounds.Y + 11, 2, 2);
                    graphics.FillEllipse(fill, bounds.X + 13, bounds.Y + 7, 2, 2);
                    break;
                case NavKind.Board:
                    graphics.DrawRectangle(pen, bounds.X, bounds.Y + 2, 7, 7);
                    graphics.DrawRectangle(pen, bounds.X + 10, bounds.Y + 2, 7, 7);
                    graphics.DrawRectangle(pen, bounds.X, bounds.Y + 13, 7, 7);
                    graphics.DrawRectangle(pen, bounds.X + 10, bounds.Y + 13, 7, 7);
                    break;
                case NavKind.Backlog:
                    graphics.DrawLine(pen, bounds.X, middleY - 7, bounds.Right, middleY - 7);
                    graphics.DrawLine(pen, bounds.X, middleY, bounds.Right, middleY);
                    graphics.DrawLine(pen, bounds.X, middleY + 7, bounds.Right, middleY + 7);
                    break;
                case NavKind.Roadmap:
                    graphics.DrawLine(pen, bounds.X + 1, bounds.Bottom - 5, bounds.Right - 1, bounds.Bottom - 5);
                    graphics.DrawLine(pen, bounds.X + 4, bounds.Y + 5, bounds.X + 4, bounds.Bottom - 5);
                    graphics.DrawLine(pen, bounds.X + 10, bounds.Y + 8, bounds.X + 10, bounds.Bottom - 5);
                    graphics.DrawLine(pen, bounds.X + 16, bounds.Y + 2, bounds.X + 16, bounds.Bottom - 5);
                    graphics.FillEllipse(fill, bounds.X + 3, bounds.Y + 4, 3, 3);
                    graphics.FillEllipse(fill, bounds.X + 9, bounds.Y + 7, 3, 3);
                    graphics.FillEllipse(fill, bounds.X + 15, bounds.Y + 1, 3, 3);
                    break;
                case NavKind.Sprints:
                    graphics.DrawLine(pen, bounds.X + 1, bounds.Y + 4, bounds.Right - 2, middleY);
                    graphics.DrawLine(pen, bounds.Right - 2, middleY, bounds.X + 1, bounds.Bottom - 4);
                    break;
                case NavKind.Issues:
                    graphics.FillEllipse(fill, bounds.X + 4, bounds.Y + 6, 10, 10);
                    break;
                case NavKind.Reports:
                    graphics.DrawLine(pen, bounds.X + 1, bounds.Bottom - 4, bounds.Right - 1, bounds.Bottom - 4);
                    graphics.FillRectangle(fill, bounds.X + 2, bounds.Bottom - 9, 3, 5);
                    graphics.FillRectangle(fill, bounds.X + 8, bounds.Bottom - 13, 3, 9);
                    graphics.FillRectangle(fill, bounds.X + 14, bounds.Bottom - 17, 3, 13);
                    graphics.DrawLine(pen, bounds.X + 2, bounds.Bottom - 12, bounds.X + 9, bounds.Bottom - 15);
                    graphics.DrawLine(pen, bounds.X + 9, bounds.Bottom - 15, bounds.Right - 2, bounds.Y + 7);
                    break;
                case NavKind.Settings:
                    graphics.DrawEllipse(pen, bounds.X + 4, bounds.Y + 6, 10, 10);
                    graphics.DrawLine(pen, bounds.X + 9, bounds.Y + 1, bounds.X + 9, bounds.Y + 5);
                    graphics.DrawLine(pen, bounds.X + 9, bounds.Bottom - 1, bounds.X + 9, bounds.Bottom - 5);
                    graphics.DrawLine(pen, bounds.X + 1, middleY, bounds.X + 5, middleY);
                    graphics.DrawLine(pen, bounds.Right - 1, middleY, bounds.Right - 5, middleY);
                    break;
            }
        }
    }

    private sealed class InitialsAvatar : Control
    {
        public InitialsAvatar(string initials, int size)
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Initials = initials;
            Size = new Size(size, size);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        public string Initials { get; }
        public Color BackCircleColor { get; set; } = JiraTheme.Blue600;
        public Color ForeTextColor { get; set; } = Color.White;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SetClip(new Rectangle(0, 0, Width, Height));
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var fill = new SolidBrush(BackCircleColor);
            e.Graphics.FillEllipse(fill, ClientRectangle);
            TextRenderer.DrawText(e.Graphics, Initials, JiraTheme.FontCaption, ClientRectangle, ForeTextColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private sealed class IssueNavigatorView : UserControl
    {
        private readonly AppSession _session;
        private readonly Label _titleLabel = JiraControlFactory.CreateLabel("Issues");
        private readonly Label _subtitleLabel = JiraControlFactory.CreateLabel("Browse every issue in the active project.", true);
        private readonly Label _countBadge = JiraControlFactory.CreateLabel("0 issues", true);
        private readonly ComboBox _statusFilter = CreateFilterCombo(150);
        private readonly ComboBox _priorityFilter = CreateFilterCombo(140);
        private readonly ComboBox _typeFilter = CreateFilterCombo(130);
        private readonly Button _clearFiltersButton = JiraControlFactory.CreateSecondaryButton("Clear filters");
        private readonly DataGridView _grid = new();
        private readonly Label _emptyState = JiraControlFactory.CreateLabel("No issues match the current filters.", true);
        private readonly Button _openButton = JiraControlFactory.CreateSecondaryButton("Open issue");

        private int _projectId;
        private IReadOnlyList<IssueSummaryDto> _issues = Array.Empty<IssueSummaryDto>();
        private string _shellSearch = string.Empty;

        public IssueNavigatorView(AppSession session)
        {
            _session = session;
            BackColor = JiraTheme.BgPage;
            Font = JiraTheme.FontBody;
            DoubleBuffered = true;

            _titleLabel.Font = JiraTheme.FontH1;
            _subtitleLabel.Font = JiraTheme.FontCaption;
            _countBadge.AutoSize = true;
            _countBadge.BackColor = JiraTheme.Blue100;
            _countBadge.ForeColor = JiraTheme.PrimaryActive;
            _countBadge.Padding = new Padding(10, 6, 10, 6);
            _countBadge.Margin = new Padding(12, 0, 0, 0);

            _statusFilter.SelectedIndexChanged += OnFilterSelectedIndexChanged;
            _priorityFilter.SelectedIndexChanged += OnFilterSelectedIndexChanged;
            _typeFilter.SelectedIndexChanged += OnFilterSelectedIndexChanged;
            _clearFiltersButton.AutoSize = false;
            _clearFiltersButton.Size = new Size(118, 36);
            _clearFiltersButton.Click += OnClearFiltersButtonClick;

            _openButton.AutoSize = false;
            _openButton.Size = new Size(116, 36);
            _openButton.Enabled = false;
            _openButton.Click += OnOpenButtonClick;

            ConfigureGrid();

            Controls.Add(BuildLayout());
            Load += OnIssueNavigatorLoad;
        }

        public async Task RefreshIssuesAsync()
        {
            try
            {
                Project? project = null;
                IReadOnlyList<BoardColumnDto> columns = Array.Empty<BoardColumnDto>();

                await _session.RunSerializedAsync(async () =>
                {
                    project = await _session.Projects.GetActiveProjectAsync();
                    if (project is null)
                    {
                        return;
                    }

                    columns = await _session.Board.GetBoardAsync(project.Id);
                });

                if (project is null)
                {
                    _projectId = 0;
                    _issues = Array.Empty<IssueSummaryDto>();
                    BindIssues();
                    return;
                }

                _projectId = project.Id;
                _issues = columns
                    .SelectMany(x => x.Issues)
                    .GroupBy(x => x.Id)
                    .Select(x => x.First())
                    .OrderByDescending(x => x.Id)
                    .ToList();
                _subtitleLabel.Text = $"Browse every issue in {project.Name}.";
                ResetFilter(_statusFilter, "All statuses", _issues.Select(x => x.StatusName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x));
                ResetFilter(_priorityFilter, "All priorities", Enum.GetNames<IssuePriority>());
                ResetFilter(_typeFilter, "All types", Enum.GetNames<IssueType>());
                BindIssues();
            }
            catch (Exception exception)
            {
                ErrorDialogService.Show(exception);
            }
        }

        public void SetShellSearch(string value)
        {
            _shellSearch = value?.Trim() ?? string.Empty;
            BindIssues();
        }

        private Control BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = JiraTheme.BgPage,
                Padding = new Padding(20, 18, 20, 20),
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildFilterBar(), 0, 1);
            root.Controls.Add(BuildSurface(), 0, 2);
            return root;
        }

        private Control BuildHeader()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = JiraTheme.BgPage };

            var right = new Panel { Dock = DockStyle.Right, Width = 140, BackColor = JiraTheme.BgPage };
            right.Controls.Add(_openButton);
            _openButton.Location = new Point(12, 18);

            var meta = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = JiraTheme.BgPage,
                Padding = new Padding(0),
                Margin = new Padding(0),
            };
            _titleLabel.Margin = new Padding(0, 0, 0, 0);
            _countBadge.Margin = new Padding(12, 6, 0, 0);
            meta.Controls.Add(_titleLabel);
            meta.Controls.Add(_countBadge);

            var left = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgPage };
            left.Controls.Add(_subtitleLabel);
            left.Controls.Add(meta);
            meta.Location = new Point(0, 0);
            _subtitleLabel.Location = new Point(0, 42);

            header.Controls.Add(right);
            header.Controls.Add(left);
            return header;
        }

        private Control BuildFilterBar()
        {
            var host = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = JiraTheme.BgPage,
                Padding = new Padding(0, 0, 0, 12),
            };

            var filterBar = new BorderFlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = JiraTheme.BgSurface,
                Padding = new Padding(12, 8, 12, 8),
                WrapContents = false,
                Margin = new Padding(0),
            };
            filterBar.Controls.Add(_statusFilter);
            filterBar.Controls.Add(_priorityFilter);
            filterBar.Controls.Add(_typeFilter);
            filterBar.Controls.Add(_clearFiltersButton);
            host.Controls.Add(filterBar);
            return host;
        }

        private Control BuildSurface()
        {
            var surface = new BorderPanel
            {
                Dock = DockStyle.Fill,
                BackColor = JiraTheme.BgSurface,
                Padding = new Padding(0),
            };

            _emptyState.Dock = DockStyle.Fill;
            _emptyState.TextAlign = ContentAlignment.MiddleCenter;
            _emptyState.Visible = false;

            surface.Controls.Add(_emptyState);
            surface.Controls.Add(_grid);
            return surface;
        }

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.MultiSelect = false;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoGenerateColumns = false;
            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            _grid.RowTemplate.Height = 36;
            JiraTheme.StyleDataGridView(_grid);
            _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _grid.ColumnHeadersHeight = 42;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.SelectionChanged += OnGridSelectionChanged;
            _grid.CellDoubleClick += OnGridCellDoubleClick;

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(IssueSummaryRow.Key),
                HeaderText = "Key",
                Width = 110,
                MinimumWidth = 100,
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(IssueSummaryRow.Summary),
                HeaderText = "Summary",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 260,
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(IssueSummaryRow.Status),
                HeaderText = "Status",
                Width = 130,
                MinimumWidth = 120,
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(IssueSummaryRow.Priority),
                HeaderText = "Priority",
                Width = 120,
                MinimumWidth = 110,
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(IssueSummaryRow.Type),
                HeaderText = "Type",
                Width = 110,
                MinimumWidth = 100,
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(IssueSummaryRow.Assignees),
                HeaderText = "Assignees",
                Width = 220,
                MinimumWidth = 180,
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _statusFilter.SelectedIndexChanged -= OnFilterSelectedIndexChanged;
                _priorityFilter.SelectedIndexChanged -= OnFilterSelectedIndexChanged;
                _typeFilter.SelectedIndexChanged -= OnFilterSelectedIndexChanged;
                _clearFiltersButton.Click -= OnClearFiltersButtonClick;
                _openButton.Click -= OnOpenButtonClick;
                _grid.SelectionChanged -= OnGridSelectionChanged;
                _grid.CellDoubleClick -= OnGridCellDoubleClick;
                Load -= OnIssueNavigatorLoad;
            }

            base.Dispose(disposing);
        }

        private async void OnIssueNavigatorLoad(object? sender, EventArgs e)
        {
            await RefreshIssuesAsync();
        }

        private void OnFilterSelectedIndexChanged(object? sender, EventArgs e)
        {
            BindIssues();
        }

        private void OnClearFiltersButtonClick(object? sender, EventArgs e)
        {
            ClearFilters();
        }

        private async void OnOpenButtonClick(object? sender, EventArgs e)
        {
            await OpenSelectedIssueAsync();
        }

        private void OnGridSelectionChanged(object? sender, EventArgs e)
        {
            _openButton.Enabled = _grid.CurrentRow?.DataBoundItem is IssueSummaryRow;
        }

        private async void OnGridCellDoubleClick(object? sender, DataGridViewCellEventArgs eventArgs)
        {
            if (eventArgs.RowIndex >= 0)
            {
                await OpenSelectedIssueAsync();
            }
        }

        private void BindIssues()
        {
            var status = _statusFilter.SelectedItem as string;
            var priority = _priorityFilter.SelectedItem as string;
            var type = _typeFilter.SelectedItem as string;
            var filtered = _issues
                .Where(issue =>
                    (string.IsNullOrWhiteSpace(_shellSearch) ||
                     issue.IssueKey.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase) ||
                     issue.Title.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase) ||
                     issue.ReporterName.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase) ||
                     issue.AssigneeNames.Any(x => x.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase))) &&
                    (string.IsNullOrWhiteSpace(status) || status.StartsWith("All ") || string.Equals(issue.StatusName, status, StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(priority) || priority.StartsWith("All ") || string.Equals(issue.Priority.ToString(), priority, StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(type) || type.StartsWith("All ") || string.Equals(issue.Type.ToString(), type, StringComparison.OrdinalIgnoreCase)))
                .Select(issue => new IssueSummaryRow(
                    issue.Id,
                    issue.IssueKey,
                    issue.Title,
                    issue.StatusName,
                    FormatPriority(issue.Priority),
                    FormatType(issue.Type),
                    issue.AssigneeNames.Count == 0 ? "Unassigned" : string.Join(", ", issue.AssigneeNames)))
                .ToList();

            _grid.DataSource = filtered;
            _emptyState.Visible = filtered.Count == 0;
            _grid.Visible = filtered.Count > 0;
            _countBadge.Text = filtered.Count == 1 ? "1 issue" : $"{filtered.Count} issues";
            _openButton.Enabled = filtered.Count > 0 && _grid.CurrentRow?.DataBoundItem is IssueSummaryRow;
        }

        private void ClearFilters()
        {
            if (_statusFilter.Items.Count > 0)
            {
                _statusFilter.SelectedIndex = 0;
            }

            if (_priorityFilter.Items.Count > 0)
            {
                _priorityFilter.SelectedIndex = 0;
            }

            if (_typeFilter.Items.Count > 0)
            {
                _typeFilter.SelectedIndex = 0;
            }

            BindIssues();
        }

        private static ComboBox CreateFilterCombo(int width)
        {
            var comboBox = new ComboBox
            {
                Width = width,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = JiraTheme.BgSurface,
                ForeColor = JiraTheme.TextPrimary,
                Font = JiraTheme.FontBody,
                IntegralHeight = false,
                Margin = new Padding(0, 0, 12, 0),
            };
            LayoutHelper.ConfigureComboBox(comboBox);
            return comboBox;
        }

        private static string FormatPriority(IssuePriority priority) => priority switch
        {
            IssuePriority.Highest => "Highest",
            _ => priority.ToString()
        };

        private static string FormatType(IssueType type) => type switch
        {
            IssueType.Task => "Task",
            IssueType.Bug => "Bug",
            IssueType.Story => "Story",
            _ => type.ToString()
        };

        private static void ResetFilter(ComboBox comboBox, string allLabel, IEnumerable<string> values)
        {
            var selected = comboBox.SelectedItem as string;
            comboBox.Items.Clear();
            comboBox.Items.Add(allLabel);
            foreach (var value in values)
            {
                comboBox.Items.Add(value);
            }

            comboBox.SelectedItem = selected is not null && comboBox.Items.Contains(selected) ? selected : allLabel;
        }
        private async Task OpenSelectedIssueAsync()
        {
            try
            {
                if (_grid.CurrentRow?.DataBoundItem is not IssueSummaryRow issue || _projectId == 0)
                {
                    return;
                }

                using var dialog = new IssueDetailsForm(_session, issue.Id, _projectId);
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    await RefreshIssuesAsync();
                }
            }
            catch (Exception exception)
            {
                ErrorDialogService.Show(exception);
            }
        }

        private sealed record IssueSummaryRow(int Id, string Key, string Summary, string Status, string Priority, string Type, string Assignees);
    }
}









































































