using JiraClone.Application.ActivityLog;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Attachments;
using JiraClone.Application.Auth;
using JiraClone.Application.Boards;
using JiraClone.Application.Comments;
using JiraClone.Application.Components;
using JiraClone.Application.Dashboard;
using JiraClone.Application.Issues;
using JiraClone.Application.Jql;
using JiraClone.Application.Labels;
using JiraClone.Application.Notifications;
using JiraClone.Application.Models;
using JiraClone.Application.Permissions;
using JiraClone.Application.Projects;
using JiraClone.Application.Roadmap;
using JiraClone.Application.Roles;
using JiraClone.Application.Sprints;
using JiraClone.Application.Users;
using JiraClone.Application.Versions;
using JiraClone.Application.Watchers;
using JiraClone.Application.Workflows;
using JiraClone.Domain.Entities;
using ProjectLabel = JiraClone.Domain.Entities.Label;
using ProjectComponent = JiraClone.Domain.Entities.Component;
using ProjectVersionEntity = JiraClone.Domain.Entities.ProjectVersion;
using JiraClone.Domain.Enums;
using JiraClone.Infrastructure.Security;
using JiraClone.Infrastructure.Storage;
using JiraClone.Persistence;
using JiraClone.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JiraClone.WinForms.Composition;

public sealed class AppSession : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDbContextFactory<JiraCloneDbContext> _dbContextFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AppSession> _logger;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private Project? _activeProject;

    public AppSession(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<JiraCloneDbContext>>();
        _loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = _loggerFactory.CreateLogger<AppSession>();

        try
        {
            using var migrationContext = _dbContextFactory.CreateDbContext();
            migrationContext.Database.Migrate();
            _logger.LogInformation("Database migration check completed successfully.");
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "Database migration failed during startup.");
            throw new InvalidOperationException("Database migration failed during startup. Verify the connection string and apply pending migrations.", exception);
        }

        CurrentUserContext = serviceProvider.GetRequiredService<CurrentUserContext>();
        Authorization = serviceProvider.GetRequiredService<AuthorizationService>();
        Authentication = new AuthenticationOperations(this);
        Projects = new ProjectQueryOperations(this);
        ProjectCommands = new ProjectCommandOperations(this);
        Permissions = new PermissionOperations(this);
        Board = new BoardQueryOperations(this);
        Dashboard = new DashboardOperations(this);
        Roadmap = new RoadmapOperations(this);
        Users = new UserQueryOperations(this);
        UserCommands = new UserCommandOperations(this);
        Issues = new IssueOperations(this);
        Jql = new JqlOperations(this);
        SavedFilters = new SavedFilterOperations(this);
        Labels = new LabelOperations(this);
        Components = new ComponentOperations(this);
        Versions = new VersionOperations(this);
        Watchers = new WatcherOperations(this);
        Notifications = new NotificationOperations(this);
        Workflows = new WorkflowOperations(this);
        Comments = new CommentOperations(this);
        Sprints = new SprintOperations(this);
        ActivityLog = new ActivityLogOperations(this);
        Attachments = new AttachmentOperations(this);
    }

    public CurrentUserContext CurrentUserContext { get; }
    public AuthorizationService Authorization { get; }
    public AuthenticationOperations Authentication { get; }
    public ProjectQueryOperations Projects { get; }
    public ProjectCommandOperations ProjectCommands { get; }
    public PermissionOperations Permissions { get; }
    public BoardQueryOperations Board { get; }
    public DashboardOperations Dashboard { get; }
    public RoadmapOperations Roadmap { get; }
    public UserQueryOperations Users { get; }
    public UserCommandOperations UserCommands { get; }
    public IssueOperations Issues { get; }
    public JqlOperations Jql { get; }
    public SavedFilterOperations SavedFilters { get; }
    public LabelOperations Labels { get; }
    public ComponentOperations Components { get; }
    public VersionOperations Versions { get; }
    public WatcherOperations Watchers { get; }
    public NotificationOperations Notifications { get; }
    public WorkflowOperations Workflows { get; }
    public CommentOperations Comments { get; }
    public SprintOperations Sprints { get; }
    public ActivityLogOperations ActivityLog { get; }
    public AttachmentOperations Attachments { get; }
    public Project? ActiveProject => _activeProject;

    public event EventHandler<ProjectChangedEventArgs>? ProjectChanged;

    internal AsyncServiceScope CreateScope() => _serviceProvider.CreateAsyncScope();

    internal ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();

    internal AuthenticationService CreateAuthenticationService(IServiceProvider services) =>
        services.GetRequiredService<AuthenticationService>();

    internal ProjectQueryService CreateProjectQueryService(IServiceProvider services) =>
        services.GetRequiredService<ProjectQueryService>();

    internal IPermissionService CreatePermissionService(IServiceProvider services) =>
        services.GetRequiredService<IPermissionService>();

    internal IProjectCommandService CreateProjectCommandService(IServiceProvider services) =>
        services.GetRequiredService<IProjectCommandService>();

    internal IBoardQueryService CreateBoardQueryService(IServiceProvider services) =>
        services.GetRequiredService<IBoardQueryService>();
    internal DashboardQueryService CreateDashboardQueryService(IServiceProvider services) =>
        services.GetRequiredService<DashboardQueryService>();
    internal IRoadmapService CreateRoadmapService(IServiceProvider services) =>
        services.GetRequiredService<IRoadmapService>();

    internal UserQueryService CreateUserQueryService(IServiceProvider services) =>
        services.GetRequiredService<UserQueryService>();

    internal UserCommandService CreateUserCommandService(IServiceProvider services) =>
        services.GetRequiredService<UserCommandService>();

    internal IWorkflowService CreateWorkflowService(IServiceProvider services) =>
        services.GetRequiredService<IWorkflowService>();

    internal IJqlService CreateJqlService(IServiceProvider services) =>
        services.GetRequiredService<IJqlService>();

    internal ISavedFilterService CreateSavedFilterService(IServiceProvider services) =>
        services.GetRequiredService<ISavedFilterService>();

    internal IssueService CreateIssueService(IServiceProvider services) =>
        services.GetRequiredService<IssueService>();

    internal IIssueRepository CreateIssueRepository(IServiceProvider services) =>
        services.GetRequiredService<IIssueRepository>();

    internal IWatcherService CreateWatcherService(IServiceProvider services) =>
        services.GetRequiredService<IWatcherService>();

    internal INotificationService CreateNotificationService(IServiceProvider services) =>
        services.GetRequiredService<INotificationService>();

    internal ILabelService CreateLabelService(IServiceProvider services) =>
        services.GetRequiredService<ILabelService>();

    internal IComponentService CreateComponentService(IServiceProvider services) =>
        services.GetRequiredService<IComponentService>();

    internal IVersionService CreateVersionService(IServiceProvider services) =>
        services.GetRequiredService<IVersionService>();

    internal CommentService CreateCommentService(IServiceProvider services) =>
        services.GetRequiredService<CommentService>();

    internal ISprintService CreateSprintService(IServiceProvider services) =>
        services.GetRequiredService<ISprintService>();

    internal ActivityLogService CreateActivityLogService(IServiceProvider services) =>
        services.GetRequiredService<ActivityLogService>();

    internal AttachmentFacade CreateAttachmentFacade(IServiceProvider services) =>
        services.GetRequiredService<AttachmentFacade>();

    public async Task<Project?> InitializeActiveProjectAsync(CancellationToken cancellationToken = default)
    {
        return await RefreshActiveProjectAsync(cancellationToken);
    }

    public async Task<Project?> SetActiveProjectAsync(int projectId, CancellationToken cancellationToken = default)
    {
        if (CurrentUserContext.CurrentUser is null)
        {
            SetActiveProjectInternal(null, raiseEvent: true);
            return null;
        }

        await using var scope = CreateScope();
        var project = await CreateProjectQueryService(scope.ServiceProvider).GetByIdAsync(projectId, cancellationToken);
        var currentUserId = CurrentUserContext.RequireUserId();
        if (project is null || !project.IsActive || project.Members.All(x => x.UserId != currentUserId))
        {
            throw new InvalidOperationException("The selected project is not available for the current user.");
        }

        SetActiveProjectInternal(project, raiseEvent: true);
        return _activeProject;
    }

    internal async Task<Project?> RefreshActiveProjectAsync(CancellationToken cancellationToken = default, bool raiseEvent = false)
    {
        if (CurrentUserContext.CurrentUser is null)
        {
            SetActiveProjectInternal(null, raiseEvent);
            return null;
        }

        var currentUserId = CurrentUserContext.RequireUserId();
        await using var scope = CreateScope();
        var projectQueryService = CreateProjectQueryService(scope.ServiceProvider);
        Project? project = null;

        if (_activeProject?.Id is int currentProjectId)
        {
            project = await projectQueryService.GetByIdAsync(currentProjectId, cancellationToken);
            if (project is not null && (!project.IsActive || project.Members.All(x => x.UserId != currentUserId)))
            {
                project = null;
            }
        }

        if (project is null)
        {
            project = (await projectQueryService.GetAccessibleProjectsAsync(currentUserId, cancellationToken)).FirstOrDefault();
        }

        SetActiveProjectInternal(project, raiseEvent);
        return _activeProject;
    }

    public async Task RunSerializedAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            await operation();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<T> RunSerializedAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            return await operation();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public void Dispose()
    {
        _operationGate.Dispose();
    }

    private void SetActiveProjectInternal(Project? project, bool raiseEvent)
    {
        var previousProject = _activeProject;
        _activeProject = project;

        if (raiseEvent && (previousProject is not null || project is not null))
        {
            ProjectChanged?.Invoke(this, new ProjectChangedEventArgs(previousProject, project));
        }
    }

    public sealed class ProjectChangedEventArgs : EventArgs
    {
        public ProjectChangedEventArgs(Project? previousProject, Project? currentProject)
        {
            PreviousProject = previousProject;
            CurrentProject = currentProject;
        }

        public Project? PreviousProject { get; }
        public Project? CurrentProject { get; }
    }

        public sealed class AuthenticationOperations
    {
        private readonly AppSession _session;

        internal AuthenticationOperations(AppSession session) => _session = session;

        public async Task<AuthResult> LoginAsync(string userName, string password, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            var result = await _session.CreateAuthenticationService(scope.ServiceProvider).LoginAsync(userName, password, cancellationToken);
            if (result.Succeeded)
            {
                _session.SetActiveProjectInternal(null, raiseEvent: false);
            }

            return result;
        }

        public async Task<SessionData> CreatePersistentSessionAsync(int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateAuthenticationService(scope.ServiceProvider).CreatePersistentSessionAsync(userId, cancellationToken);
        }

        public async Task<bool> ValidateRefreshTokenAsync(int userId, string refreshToken, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            var restored = await _session.CreateAuthenticationService(scope.ServiceProvider).ValidateRefreshTokenAsync(userId, refreshToken, cancellationToken);
            if (restored)
            {
                _session.SetActiveProjectInternal(null, raiseEvent: false);
            }

            return restored;
        }

        public async Task ClearPersistentSessionAsync(int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            await _session.CreateAuthenticationService(scope.ServiceProvider).ClearPersistentSessionAsync(userId, cancellationToken);
            if (_session.CurrentUserContext.CurrentUser is null)
            {
                _session.SetActiveProjectInternal(null, raiseEvent: false);
            }
        }
    }

        public sealed class ProjectQueryOperations
    {
        private readonly AppSession _session;

        internal ProjectQueryOperations(AppSession session) => _session = session;

        public async Task<Project?> GetActiveProjectAsync(CancellationToken cancellationToken = default)
        {
            return await _session.RefreshActiveProjectAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Project>> GetAccessibleProjectsAsync(CancellationToken cancellationToken = default)
        {
            if (_session.CurrentUserContext.CurrentUser is null)
            {
                return [];
            }

            await using var scope = _session.CreateScope();
            return await _session.CreateProjectQueryService(scope.ServiceProvider).GetAccessibleProjectsAsync(_session.CurrentUserContext.RequireUserId(), cancellationToken);
        }

        public async Task<Project?> GetByIdAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateProjectQueryService(scope.ServiceProvider).GetByIdAsync(projectId, cancellationToken);
        }
    }
    public sealed class ProjectCommandOperations
    {
        private readonly AppSession _session;

        internal ProjectCommandOperations(AppSession session) => _session = session;

        public async Task<Project> CreateProjectAsync(string name, string key, ProjectCategory category, string? description, IReadOnlyCollection<ProjectMemberInput> members, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateProjectCommandService(scope.ServiceProvider).CreateProjectAsync(name, key, category, description, members, cancellationToken);
        }

        public async Task<bool> ProjectKeyExistsAsync(string key, int? excludeProjectId = null, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateProjectCommandService(scope.ServiceProvider).ProjectKeyExistsAsync(key, excludeProjectId, cancellationToken);
        }

        public async Task<Project?> UpdateProjectAsync(int projectId, string name, string? description, ProjectCategory category, BoardType boardType, string? url, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            var project = await _session.CreateProjectCommandService(scope.ServiceProvider).UpdateProjectAsync(projectId, name, description, category, boardType, url, cancellationToken);
            if (project is not null && _session.ActiveProject?.Id == project.Id)
            {
                _session.SetActiveProjectInternal(project, raiseEvent: true);
            }

            return project;
        }

        public async Task<bool> ArchiveProjectAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            var archived = await _session.CreateProjectCommandService(scope.ServiceProvider).ArchiveProjectAsync(projectId, cancellationToken);
            if (archived)
            {
                await _session.RefreshActiveProjectAsync(cancellationToken, raiseEvent: true);
            }

            return archived;
        }

        public async Task<bool> DeleteProjectAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            var deleted = await _session.CreateProjectCommandService(scope.ServiceProvider).DeleteProjectAsync(projectId, _session.CurrentUserContext.RequireUserId(), cancellationToken);
            if (!deleted)
            {
                return false;
            }

            if (_session.ActiveProject?.Id == projectId)
            {
                _session.SetActiveProjectInternal(null, raiseEvent: true);
            }
            else
            {
                await _session.RefreshActiveProjectAsync(cancellationToken, raiseEvent: true);
            }

            return true;
        }

        public async Task<bool> AddMemberAsync(int projectId, int userId, ProjectRole projectRole, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            var added = await _session.CreateProjectCommandService(scope.ServiceProvider).AddMemberAsync(projectId, userId, projectRole, cancellationToken);
            if (added && _session.ActiveProject?.Id == projectId)
            {
                await _session.RefreshActiveProjectAsync(cancellationToken, raiseEvent: true);
            }

            return added;
        }

        public async Task<bool> UpdateMemberRoleAsync(int projectId, int userId, ProjectRole projectRole, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            var updated = await _session.CreateProjectCommandService(scope.ServiceProvider).UpdateMemberRoleAsync(projectId, userId, projectRole, cancellationToken);
            if (updated && _session.ActiveProject?.Id == projectId)
            {
                await _session.RefreshActiveProjectAsync(cancellationToken, raiseEvent: true);
            }

            return updated;
        }

        public async Task<bool> RemoveMemberAsync(int projectId, int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            var removed = await _session.CreateProjectCommandService(scope.ServiceProvider).RemoveMemberAsync(projectId, userId, cancellationToken);
            if (removed && (_session.ActiveProject?.Id == projectId || _session.CurrentUserContext.CurrentUser?.Id == userId))
            {
                await _session.RefreshActiveProjectAsync(cancellationToken, raiseEvent: true);
            }

            return removed;
        }

        public async Task<bool> UpdateBoardColumnAsync(int projectId, int boardColumnId, string name, int? wipLimit, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            var updated = await _session.CreateProjectCommandService(scope.ServiceProvider).UpdateBoardColumnAsync(projectId, boardColumnId, name, wipLimit, cancellationToken);
            if (updated && _session.ActiveProject?.Id == projectId)
            {
                await _session.RefreshActiveProjectAsync(cancellationToken, raiseEvent: true);
            }

            return updated;
        }

        public async Task<bool> UpdatePermissionSchemeAsync(int projectId, string name, IReadOnlyCollection<PermissionGrantInput> grants, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            var updated = await _session.CreateProjectCommandService(scope.ServiceProvider).UpdatePermissionSchemeAsync(projectId, name, grants, cancellationToken);
            if (updated && _session.ActiveProject?.Id == projectId)
            {
                await _session.RefreshActiveProjectAsync(cancellationToken, raiseEvent: true);
            }

            return updated;
        }
    }

    public sealed class PermissionOperations
    {
        private readonly AppSession _session;

        internal PermissionOperations(AppSession session) => _session = session;

        public async Task<bool> HasPermissionAsync(int userId, int projectId, Permission permission, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreatePermissionService(scope.ServiceProvider).HasPermissionAsync(userId, projectId, permission, cancellationToken);
        }

        public async Task<IReadOnlyList<Permission>> GetUserPermissionsAsync(int userId, int projectId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreatePermissionService(scope.ServiceProvider).GetUserPermissionsAsync(userId, projectId, cancellationToken);
        }
    }

    public sealed class BoardQueryOperations
    {
        private readonly AppSession _session;

        internal BoardQueryOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<BoardColumnDto>> GetBoardAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateBoardQueryService(scope.ServiceProvider).GetBoardAsync(projectId, cancellationToken);
        }

        public async Task<IReadOnlyList<BoardColumnDto>> GetBoardAsync(int projectId, int? sprintId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateBoardQueryService(scope.ServiceProvider).GetBoardAsync(projectId, sprintId, cancellationToken);
        }

        public async Task<TimeSpan?> GetAverageCycleTimeAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateBoardQueryService(scope.ServiceProvider).GetAverageCycleTimeAsync(projectId, cancellationToken);
        }
    }

    public sealed class DashboardOperations
    {
        private readonly AppSession _session;

        internal DashboardOperations(AppSession session) => _session = session;

        public async Task<DashboardOverviewDto> GetOverviewAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateDashboardQueryService(scope.ServiceProvider).GetOverviewAsync(projectId, cancellationToken);
        }
    }

    public sealed class RoadmapOperations
    {
        private readonly AppSession _session;
        internal RoadmapOperations(AppSession session) => _session = session;
        public async Task<IReadOnlyList<RoadmapEpicDto>> GetEpicsForRoadmapAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateRoadmapService(scope.ServiceProvider).GetEpicsForRoadmapAsync(projectId, cancellationToken);
        }
    }
    public sealed class UserQueryOperations
    {
        private readonly AppSession _session;
        internal UserQueryOperations(AppSession session) => _session = session;
        public async Task<IReadOnlyList<User>> GetProjectUsersAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateUserQueryService(scope.ServiceProvider).GetProjectUsersAsync(projectId, cancellationToken);
        }
    }
    public sealed class UserCommandOperations
    {
        private readonly AppSession _session;

        internal UserCommandOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateUserCommandService(scope.ServiceProvider).GetAllAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateUserCommandService(scope.ServiceProvider).GetRolesAsync(cancellationToken);
        }

        public async Task<User> CreateAsync(int projectId, string userName, string displayName, string email, string password, ProjectRole projectRole, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateUserCommandService(scope.ServiceProvider).CreateAsync(projectId, userName, displayName, email, password, projectRole, roleNames, cancellationToken);
        }

        public async Task<User?> UpdateAsync(int userId, string displayName, string email, bool isActive, ProjectRole? projectRole, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateUserCommandService(scope.ServiceProvider).UpdateAsync(userId, displayName, email, isActive, projectRole, roleNames, cancellationToken);
        }

        public async Task<bool> ResetPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateUserCommandService(scope.ServiceProvider).ResetPasswordAsync(userId, newPassword, cancellationToken);
        }

        public async Task<User?> DeactivateAsync(int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateUserCommandService(scope.ServiceProvider).DeactivateAsync(userId, cancellationToken);
        }

        public async Task<User?> ActivateAsync(int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateUserCommandService(scope.ServiceProvider).ActivateAsync(userId, cancellationToken);
        }

        public async Task<User?> UpdateEmailNotificationsPreferenceAsync(int userId, bool isEnabled, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateUserCommandService(scope.ServiceProvider).UpdateEmailNotificationsPreferenceAsync(userId, isEnabled, cancellationToken);
        }
    }

    public sealed class IssueOperations
    {
        private readonly AppSession _session;

        internal IssueOperations(AppSession session) => _session = session;

        public async Task<Issue> CreateAsync(IssueEditModel model, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateIssueService(scope.ServiceProvider).CreateAsync(model, cancellationToken);
        }

        public async Task<Issue?> UpdateAsync(IssueEditModel model, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateIssueService(scope.ServiceProvider).UpdateAsync(model, cancellationToken);
        }

        public async Task<Issue?> UpdateDueDateAsync(int issueId, DateOnly? dueDate, int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateIssueService(scope.ServiceProvider).UpdateDueDateAsync(issueId, dueDate, userId, cancellationToken);
        }
        public async Task<Issue?> UpdateScheduleAsync(int issueId, DateOnly? startDate, DateOnly? dueDate, int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateIssueService(scope.ServiceProvider).UpdateScheduleAsync(issueId, startDate, dueDate, userId, cancellationToken);
        }
        public async Task<Issue?> UpdateParentAsync(int issueId, int? parentIssueId, int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateIssueService(scope.ServiceProvider).UpdateParentAsync(issueId, parentIssueId, userId, cancellationToken);
        }

        public async Task<bool> MoveAsync(int issueId, int targetStatusId, decimal boardPosition, int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateIssueService(scope.ServiceProvider).MoveAsync(issueId, targetStatusId, boardPosition, userId, cancellationToken);
        }

        public async Task<IssueDetailsDto?> GetDetailsAsync(int issueId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateIssueService(scope.ServiceProvider).GetDetailsAsync(issueId, cancellationToken);
        }

        public async Task<bool> DeleteAsync(int issueId, int? userId = null, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateIssueService(scope.ServiceProvider).DeleteAsync(issueId, userId, cancellationToken);
        }

        public async Task<IReadOnlyList<Issue>> GetPotentialParentsAsync(int projectId, IssueType childType, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateIssueRepository(scope.ServiceProvider).GetPotentialParentsAsync(projectId, childType, cancellationToken);
        }

        public async Task<IReadOnlyList<Issue>> GetSubIssuesAsync(int issueId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateIssueRepository(scope.ServiceProvider).GetSubIssuesAsync(issueId, cancellationToken);
        }
    }

    public sealed class JqlOperations
    {
        private readonly AppSession _session;

        internal JqlOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<IssueDto>> ExecuteQueryAsync(string? jql, int projectId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateJqlService(scope.ServiceProvider).ExecuteQueryAsync(jql, projectId, cancellationToken);
        }

        public JqlQuery Parse(string? jql)
        {
            using var scope = _session.CreateScope();
            return _session.CreateJqlService(scope.ServiceProvider).Parse(jql);
        }
    }

    public sealed class SavedFilterOperations
    {
        private readonly AppSession _session;

        internal SavedFilterOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<SavedFilterDto>> GetByProjectAsync(int projectId, int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateSavedFilterService(scope.ServiceProvider).GetByProjectAsync(projectId, userId, cancellationToken);
        }

        public async Task<SavedFilterDto> CreateAsync(int projectId, int userId, string name, string queryText, bool isFavorite = false, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateSavedFilterService(scope.ServiceProvider).CreateAsync(projectId, userId, name, queryText, isFavorite, cancellationToken);
        }

        public async Task<SavedFilterDto?> UpdateAsync(int savedFilterId, int userId, string name, string queryText, bool isFavorite = false, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateSavedFilterService(scope.ServiceProvider).UpdateAsync(savedFilterId, userId, name, queryText, isFavorite, cancellationToken);
        }

        public async Task<bool> DeleteAsync(int savedFilterId, int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateSavedFilterService(scope.ServiceProvider).DeleteAsync(savedFilterId, userId, cancellationToken);
        }
    }


    public sealed class WatcherOperations
    {
        private readonly AppSession _session;

        internal WatcherOperations(AppSession session) => _session = session;

        public async Task<bool> WatchIssueAsync(int issueId, int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateWatcherService(scope.ServiceProvider).WatchIssueAsync(issueId, userId, cancellationToken);
        }

        public async Task<bool> UnwatchIssueAsync(int issueId, int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateWatcherService(scope.ServiceProvider).UnwatchIssueAsync(issueId, userId, cancellationToken);
        }

        public async Task<IReadOnlyList<User>> GetWatchersAsync(int issueId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateWatcherService(scope.ServiceProvider).GetWatchersAsync(issueId, cancellationToken);
        }

        public async Task<bool> IsWatchingAsync(int issueId, int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateWatcherService(scope.ServiceProvider).IsWatchingAsync(issueId, userId, cancellationToken);
        }
    }

    public sealed class NotificationOperations
    {
        private readonly AppSession _session;

        internal NotificationOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<NotificationItemDto>> GetUnreadAsync(int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateNotificationService(scope.ServiceProvider).GetUnreadAsync(userId, cancellationToken);
        }

        public async Task<IReadOnlyList<NotificationItemDto>> GetRecentAsync(int userId, int take = 20, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateNotificationService(scope.ServiceProvider).GetRecentAsync(userId, take, cancellationToken);
        }

        public async Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateNotificationService(scope.ServiceProvider).GetUnreadCountAsync(userId, cancellationToken);
        }

        public async Task<bool> MarkReadAsync(int notificationId, int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateNotificationService(scope.ServiceProvider).MarkReadAsync(notificationId, userId, cancellationToken);
        }

        public async Task<int> MarkAllReadAsync(int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateNotificationService(scope.ServiceProvider).MarkAllReadAsync(userId, cancellationToken);
        }
    }

    public sealed class LabelOperations
    {
        private readonly AppSession _session;

        internal LabelOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<ProjectLabel>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateLabelService(scope.ServiceProvider).GetByProjectAsync(projectId, cancellationToken);
        }

        public async Task<ProjectLabel> CreateAsync(int projectId, string name, string color, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateLabelService(scope.ServiceProvider).CreateAsync(projectId, name, color, cancellationToken);
        }

        public async Task<ProjectLabel?> UpdateAsync(int labelId, string name, string color, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateLabelService(scope.ServiceProvider).UpdateAsync(labelId, name, color, cancellationToken);
        }

        public async Task<bool> DeleteAsync(int labelId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateLabelService(scope.ServiceProvider).DeleteAsync(labelId, cancellationToken);
        }

        public async Task<bool> AssignToIssueAsync(int issueId, IReadOnlyCollection<int> labelIds, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateLabelService(scope.ServiceProvider).AssignToIssueAsync(issueId, labelIds, cancellationToken);
        }
    }

    public sealed class ComponentOperations
    {
        private readonly AppSession _session;

        internal ComponentOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<ProjectComponent>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateComponentService(scope.ServiceProvider).GetByProjectAsync(projectId, cancellationToken);
        }

        public async Task<ProjectComponent> CreateAsync(int projectId, string name, string? description, int? leadUserId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateComponentService(scope.ServiceProvider).CreateAsync(projectId, name, description, leadUserId, cancellationToken);
        }

        public async Task<ProjectComponent?> UpdateAsync(int componentId, string name, string? description, int? leadUserId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateComponentService(scope.ServiceProvider).UpdateAsync(componentId, name, description, leadUserId, cancellationToken);
        }

        public async Task<bool> DeleteAsync(int componentId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateComponentService(scope.ServiceProvider).DeleteAsync(componentId, cancellationToken);
        }

        public async Task<bool> AssignToIssueAsync(int issueId, int? componentId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateComponentService(scope.ServiceProvider).AssignToIssueAsync(issueId, componentId, cancellationToken);
        }
    }

    public sealed class VersionOperations
    {
        private readonly AppSession _session;

        internal VersionOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<ProjectVersionEntity>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateVersionService(scope.ServiceProvider).GetByProjectAsync(projectId, cancellationToken);
        }

        public async Task<ProjectVersionEntity> CreateAsync(int projectId, string name, string? description, DateTime? releaseDate, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateVersionService(scope.ServiceProvider).CreateAsync(projectId, name, description, releaseDate, cancellationToken);
        }

        public async Task<ProjectVersionEntity?> UpdateAsync(int versionId, string name, string? description, DateTime? releaseDate, bool isReleased, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateVersionService(scope.ServiceProvider).UpdateAsync(versionId, name, description, releaseDate, isReleased, cancellationToken);
        }

        public async Task<bool> DeleteAsync(int versionId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateVersionService(scope.ServiceProvider).DeleteAsync(versionId, cancellationToken);
        }

        public async Task<bool> AssignToIssueAsync(int issueId, int? versionId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateVersionService(scope.ServiceProvider).AssignToIssueAsync(issueId, versionId, cancellationToken);
        }

        public async Task<ProjectVersionEntity?> MarkReleasedAsync(int versionId, DateTime? releaseDate = null, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateVersionService(scope.ServiceProvider).MarkReleasedAsync(versionId, releaseDate, cancellationToken);
        }
    }


    public sealed class WorkflowOperations
    {
        private readonly AppSession _session;

        internal WorkflowOperations(AppSession session) => _session = session;

        public async Task<WorkflowDefinitionDto?> GetDefaultWorkflowAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateWorkflowService(scope.ServiceProvider).GetDefaultWorkflowAsync(projectId, cancellationToken);
        }

        public async Task<IReadOnlyList<WorkflowStatusOptionDto>> GetAllowedTransitionsAsync(int issueId, int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateWorkflowService(scope.ServiceProvider).GetAllowedTransitionsAsync(issueId, userId, cancellationToken);
        }

        public async Task<WorkflowTransitionResult> ExecuteTransitionAsync(int issueId, int toStatusId, int userId, decimal? boardPosition = null, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateWorkflowService(scope.ServiceProvider).ExecuteTransitionAsync(issueId, toStatusId, userId, boardPosition, cancellationToken);
        }

        public async Task<WorkflowStatus> CreateStatusAsync(int projectId, string name, string color, StatusCategory category, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateWorkflowService(scope.ServiceProvider).CreateStatusAsync(projectId, name, color, category, cancellationToken);
        }

        public async Task<WorkflowStatus?> UpdateStatusAsync(int workflowStatusId, string name, string color, StatusCategory category, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateWorkflowService(scope.ServiceProvider).UpdateStatusAsync(workflowStatusId, name, color, category, cancellationToken);
        }

        public async Task<bool> DeleteStatusAsync(int workflowStatusId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateWorkflowService(scope.ServiceProvider).DeleteStatusAsync(workflowStatusId, cancellationToken);
        }

        public async Task<WorkflowTransition?> CreateTransitionAsync(int projectId, int fromStatusId, int toStatusId, string name, IReadOnlyCollection<string> allowedRoleNames, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateWorkflowService(scope.ServiceProvider).CreateTransitionAsync(projectId, fromStatusId, toStatusId, name, allowedRoleNames, cancellationToken);
        }

        public async Task<WorkflowTransition?> UpdateTransitionAsync(int transitionId, string name, IReadOnlyCollection<string> allowedRoleNames, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateWorkflowService(scope.ServiceProvider).UpdateTransitionAsync(transitionId, name, allowedRoleNames, cancellationToken);
        }

        public async Task<bool> DeleteTransitionAsync(int transitionId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateWorkflowService(scope.ServiceProvider).DeleteTransitionAsync(transitionId, cancellationToken);
        }
    }
    public sealed class CommentOperations
    {
        private readonly AppSession _session;

        internal CommentOperations(AppSession session) => _session = session;

        public async Task<Comment> AddAsync(int issueId, int userId, int projectId, string body, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateCommentService(scope.ServiceProvider).AddAsync(issueId, userId, projectId, body, cancellationToken);
        }

        public async Task<Comment?> UpdateAsync(int commentId, int userId, string body, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateCommentService(scope.ServiceProvider).UpdateAsync(commentId, userId, body, cancellationToken);
        }

        public async Task<bool> SoftDeleteAsync(int commentId, int userId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateCommentService(scope.ServiceProvider).SoftDeleteAsync(commentId, userId, cancellationToken);
        }
    }

    public sealed class SprintOperations
    {
        private readonly AppSession _session;

        internal SprintOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<Sprint>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateSprintService(scope.ServiceProvider).GetByProjectAsync(projectId, cancellationToken);
        }

        public async Task<Sprint?> GetActiveByProjectAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateSprintService(scope.ServiceProvider).GetActiveByProjectAsync(projectId, cancellationToken);
        }

        public async Task<IReadOnlyList<Issue>> GetAssignableIssuesAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateSprintService(scope.ServiceProvider).GetAssignableIssuesAsync(projectId, cancellationToken);
        }

        public async Task<Sprint> CreateAsync(int projectId, string name, string? goal, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateSprintService(scope.ServiceProvider).CreateAsync(projectId, name, goal, startDate, endDate, cancellationToken);
        }

        public async Task<bool> AssignIssuesAsync(int sprintId, IReadOnlyCollection<int> issueIds, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateSprintService(scope.ServiceProvider).AssignIssuesAsync(sprintId, issueIds, cancellationToken);
        }

        public async Task<bool> StartSprintAsync(int sprintId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateSprintService(scope.ServiceProvider).StartSprintAsync(sprintId, cancellationToken);
        }

        public async Task<bool> CloseSprintAsync(int sprintId, int? moveToSprintId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateSprintService(scope.ServiceProvider).CloseSprintAsync(sprintId, moveToSprintId, cancellationToken);
        }

        public async Task<BurndownReportDto?> GetBurndownDataAsync(int sprintId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateSprintService(scope.ServiceProvider).GetBurndownDataAsync(sprintId, cancellationToken);
        }

        public async Task<VelocityReportDto> GetVelocityDataAsync(int projectId, int lastN = 6, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateSprintService(scope.ServiceProvider).GetVelocityDataAsync(projectId, lastN, cancellationToken);
        }

        public async Task<IReadOnlyList<CfdDataPointDto>> GetCfdDataAsync(int projectId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateSprintService(scope.ServiceProvider).GetCfdDataAsync(projectId, from, to, cancellationToken);
        }

        public async Task<SprintReportDto?> GetSprintReportAsync(int sprintId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateSprintService(scope.ServiceProvider).GetSprintReportAsync(sprintId, cancellationToken);
        }
    }

    public sealed class ActivityLogOperations
    {
        private readonly AppSession _session;

        internal ActivityLogOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<JiraClone.Domain.Entities.ActivityLog>> GetIssueActivityAsync(int issueId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateActivityLogService(scope.ServiceProvider).GetIssueActivityAsync(issueId, cancellationToken);
        }
    }

    public sealed class AttachmentOperations
    {
        private readonly AppSession _session;

        internal AttachmentOperations(AppSession session) => _session = session;

        public async Task<Attachment> AddAsync(int issueId, int projectId, int userId, string sourceFilePath, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateAttachmentFacade(scope.ServiceProvider).AddAsync(issueId, projectId, userId, sourceFilePath, cancellationToken);
        }

        public async Task<IReadOnlyList<Attachment>> GetByIssueAsync(int issueId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateAttachmentFacade(scope.ServiceProvider).GetByIssueAsync(issueId, cancellationToken);
        }

        public async Task<string?> ResolveDownloadPathAsync(int attachmentId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateAttachmentFacade(scope.ServiceProvider).ResolveDownloadPathAsync(attachmentId, cancellationToken);
        }

        public async Task<bool> SoftDeleteAsync(int attachmentId, int userId, int projectId, CancellationToken cancellationToken = default)
        {
            await using var scope = _session.CreateScope();
            return await _session.CreateAttachmentFacade(scope.ServiceProvider).SoftDeleteAsync(attachmentId, userId, projectId, cancellationToken);
        }
    }
}











































