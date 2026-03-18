using JiraClone.Application.ActivityLog;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Attachments;
using JiraClone.Application.Auth;
using JiraClone.Application.Boards;
using JiraClone.Application.Comments;
using JiraClone.Application.Components;
using JiraClone.Application.Issues;
using JiraClone.Application.Jql;
using JiraClone.Application.Labels;
using JiraClone.Application.Models;
using JiraClone.Application.Projects;
using JiraClone.Application.Roles;
using JiraClone.Application.Sprints;
using JiraClone.Application.Users;
using JiraClone.Application.Versions;
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
using Microsoft.Extensions.Logging;

namespace JiraClone.WinForms.Composition;

public sealed class AppSession : IDisposable
{
    private readonly IDbContextFactory<JiraCloneDbContext> _dbContextFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AppSession> _logger;
    private readonly IPasswordHasher _passwordHasher;
    private readonly FileShareAttachmentService _attachmentStorage;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private Project? _activeProject;

    public AppSession(IDbContextFactory<JiraCloneDbContext> dbContextFactory, ILoggerFactory loggerFactory, string attachmentRootPath, long maxAttachmentSizeBytes)
    {
        _dbContextFactory = dbContextFactory;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<AppSession>();
        _passwordHasher = new Sha256PasswordHasher();
        _attachmentStorage = new FileShareAttachmentService(attachmentRootPath, maxAttachmentSizeBytes, loggerFactory.CreateLogger<FileShareAttachmentService>());

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

        CurrentUserContext = new CurrentUserContext();
        Authorization = new AuthorizationService(CurrentUserContext);
        Authentication = new AuthenticationOperations(this);
        Projects = new ProjectQueryOperations(this);
        ProjectCommands = new ProjectCommandOperations(this);
        Board = new BoardQueryOperations(this);
        Users = new UserQueryOperations(this);
        UserCommands = new UserCommandOperations(this);
        Issues = new IssueOperations(this);
        Jql = new JqlOperations(this);
        SavedFilters = new SavedFilterOperations(this);
        Labels = new LabelOperations(this);
        Components = new ComponentOperations(this);
        Versions = new VersionOperations(this);
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
    public BoardQueryOperations Board { get; }
    public UserQueryOperations Users { get; }
    public UserCommandOperations UserCommands { get; }
    public IssueOperations Issues { get; }
    public JqlOperations Jql { get; }
    public SavedFilterOperations SavedFilters { get; }
    public LabelOperations Labels { get; }
    public ComponentOperations Components { get; }
    public VersionOperations Versions { get; }
    public WorkflowOperations Workflows { get; }
    public CommentOperations Comments { get; }
    public SprintOperations Sprints { get; }
    public ActivityLogOperations ActivityLog { get; }
    public AttachmentOperations Attachments { get; }
    public Project? ActiveProject => _activeProject;

    public event EventHandler<ProjectChangedEventArgs>? ProjectChanged;

    internal JiraCloneDbContext CreateDbContext() => _dbContextFactory.CreateDbContext();

    internal ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();

    internal AuthenticationService CreateAuthenticationService(JiraCloneDbContext dbContext) =>
        new(new UserRepository(dbContext), _passwordHasher, CurrentUserContext, CreateLogger<AuthenticationService>());

    internal ProjectQueryService CreateProjectQueryService(JiraCloneDbContext dbContext) =>
        new(new ProjectRepository(dbContext), CreateLogger<ProjectQueryService>());

    internal IProjectCommandService CreateProjectCommandService(JiraCloneDbContext dbContext) =>
        new ProjectCommandService(new ProjectRepository(dbContext), new UserRepository(dbContext), Authorization, new ActivityLogRepository(dbContext), CurrentUserContext, new UnitOfWork(dbContext), CreateLogger<ProjectCommandService>());

    internal BoardQueryService CreateBoardQueryService(JiraCloneDbContext dbContext) =>
        new(new IssueRepository(dbContext), new ProjectRepository(dbContext), CreateLogger<BoardQueryService>());

    internal UserQueryService CreateUserQueryService(JiraCloneDbContext dbContext) =>
        new(new UserRepository(dbContext), CreateLogger<UserQueryService>());

    internal UserCommandService CreateUserCommandService(JiraCloneDbContext dbContext) =>
        new(new UserRepository(dbContext), new ProjectRepository(dbContext), _passwordHasher, Authorization, new ActivityLogRepository(dbContext), CurrentUserContext, new UnitOfWork(dbContext), CreateLogger<UserCommandService>());

    internal IWorkflowService CreateWorkflowService(JiraCloneDbContext dbContext) =>
        new WorkflowService(
            new WorkflowRepository(dbContext),
            new IssueRepository(dbContext),
            new ProjectRepository(dbContext),
            new UserRepository(dbContext),
            Authorization,
            new ActivityLogRepository(dbContext),
            CurrentUserContext,
            new UnitOfWork(dbContext),
            CreateLogger<WorkflowService>());

    internal IJqlService CreateJqlService(JiraCloneDbContext dbContext) =>
        new JqlService(new IssueRepository(dbContext), CurrentUserContext, CreateLogger<JqlService>());

    internal ISavedFilterService CreateSavedFilterService(JiraCloneDbContext dbContext) =>
        new SavedFilterService(new SavedFilterRepository(dbContext), new ProjectRepository(dbContext), Authorization, new UnitOfWork(dbContext), CreateLogger<SavedFilterService>());

    internal IssueService CreateIssueService(JiraCloneDbContext dbContext) =>
        new(
            new IssueRepository(dbContext),
            new UserRepository(dbContext),
            new ProjectRepository(dbContext),
            new CommentRepository(dbContext),
            new AttachmentRepository(dbContext),
            Authorization,
            new ActivityLogRepository(dbContext),
            CreateWorkflowService(dbContext),
            new WorkflowRepository(dbContext),
            new UnitOfWork(dbContext),
            CreateLogger<IssueService>());

    internal ILabelService CreateLabelService(JiraCloneDbContext dbContext) =>
        new LabelService(
            new LabelRepository(dbContext),
            new IssueRepository(dbContext),
            new ProjectRepository(dbContext),
            Authorization,
            new ActivityLogRepository(dbContext),
            CurrentUserContext,
            new UnitOfWork(dbContext),
            CreateLogger<LabelService>());

    internal IComponentService CreateComponentService(JiraCloneDbContext dbContext) =>
        new ComponentService(
            new ComponentRepository(dbContext),
            new IssueRepository(dbContext),
            new ProjectRepository(dbContext),
            new UserRepository(dbContext),
            Authorization,
            new ActivityLogRepository(dbContext),
            CurrentUserContext,
            new UnitOfWork(dbContext),
            CreateLogger<ComponentService>());

    internal IVersionService CreateVersionService(JiraCloneDbContext dbContext) =>
        new VersionService(
            new ProjectVersionRepository(dbContext),
            new IssueRepository(dbContext),
            new ProjectRepository(dbContext),
            Authorization,
            new ActivityLogRepository(dbContext),
            CurrentUserContext,
            new UnitOfWork(dbContext),
            CreateLogger<VersionService>());

    internal CommentService CreateCommentService(JiraCloneDbContext dbContext) =>
        new(new CommentRepository(dbContext), Authorization, new ActivityLogRepository(dbContext), new UnitOfWork(dbContext), CreateLogger<CommentService>());

    internal ISprintService CreateSprintService(JiraCloneDbContext dbContext) =>
        new SprintService(new SprintRepository(dbContext), new IssueRepository(dbContext), new WorkflowRepository(dbContext), Authorization, new ActivityLogRepository(dbContext), CurrentUserContext, new UnitOfWork(dbContext), CreateLogger<SprintService>());

    internal ActivityLogService CreateActivityLogService(JiraCloneDbContext dbContext) =>
        new(new ActivityLogRepository(dbContext), CreateLogger<ActivityLogService>());

    internal AttachmentFacade CreateAttachmentFacade(JiraCloneDbContext dbContext) =>
        new(_attachmentStorage, new AttachmentRepository(dbContext), Authorization, new ActivityLogRepository(dbContext), new UnitOfWork(dbContext), CreateLogger<AttachmentFacade>());

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

        await using var dbContext = CreateDbContext();
        var project = await CreateProjectQueryService(dbContext).GetByIdAsync(projectId, cancellationToken);
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
        await using var dbContext = CreateDbContext();
        var projectQueryService = CreateProjectQueryService(dbContext);
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
            await using var dbContext = _session.CreateDbContext();
            var result = await _session.CreateAuthenticationService(dbContext).LoginAsync(userName, password, cancellationToken);
            if (result.Succeeded)
            {
                _session.SetActiveProjectInternal(null, raiseEvent: false);
            }

            return result;
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

            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateProjectQueryService(dbContext).GetAccessibleProjectsAsync(_session.CurrentUserContext.RequireUserId(), cancellationToken);
        }

        public async Task<Project?> GetByIdAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateProjectQueryService(dbContext).GetByIdAsync(projectId, cancellationToken);
        }
    }

        public sealed class ProjectCommandOperations
    {
        private readonly AppSession _session;

        internal ProjectCommandOperations(AppSession session) => _session = session;

        public async Task<Project> CreateProjectAsync(string name, string key, ProjectCategory category, string? description, IReadOnlyCollection<ProjectMemberInput> members, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateProjectCommandService(dbContext).CreateProjectAsync(name, key, category, description, members, cancellationToken);
        }

        public async Task<bool> ProjectKeyExistsAsync(string key, int? excludeProjectId = null, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateProjectCommandService(dbContext).ProjectKeyExistsAsync(key, excludeProjectId, cancellationToken);
        }

        public async Task<Project?> UpdateProjectAsync(int projectId, string name, string? description, ProjectCategory category, string? url, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            var project = await _session.CreateProjectCommandService(dbContext).UpdateProjectAsync(projectId, name, description, category, url, cancellationToken);
            if (project is not null && _session.ActiveProject?.Id == project.Id)
            {
                _session.SetActiveProjectInternal(project, raiseEvent: true);
            }

            return project;
        }

        public async Task<bool> ArchiveProjectAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            var archived = await _session.CreateProjectCommandService(dbContext).ArchiveProjectAsync(projectId, cancellationToken);
            if (archived)
            {
                await _session.RefreshActiveProjectAsync(cancellationToken, raiseEvent: true);
            }

            return archived;
        }

        public async Task<bool> AddMemberAsync(int projectId, int userId, ProjectRole projectRole, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            var added = await _session.CreateProjectCommandService(dbContext).AddMemberAsync(projectId, userId, projectRole, cancellationToken);
            if (added && _session.ActiveProject?.Id == projectId)
            {
                await _session.RefreshActiveProjectAsync(cancellationToken, raiseEvent: true);
            }

            return added;
        }

        public async Task<bool> UpdateMemberRoleAsync(int projectId, int userId, ProjectRole projectRole, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            var updated = await _session.CreateProjectCommandService(dbContext).UpdateMemberRoleAsync(projectId, userId, projectRole, cancellationToken);
            if (updated && _session.ActiveProject?.Id == projectId)
            {
                await _session.RefreshActiveProjectAsync(cancellationToken, raiseEvent: true);
            }

            return updated;
        }

        public async Task<bool> RemoveMemberAsync(int projectId, int userId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            var removed = await _session.CreateProjectCommandService(dbContext).RemoveMemberAsync(projectId, userId, cancellationToken);
            if (removed && (_session.ActiveProject?.Id == projectId || _session.CurrentUserContext.CurrentUser?.Id == userId))
            {
                await _session.RefreshActiveProjectAsync(cancellationToken, raiseEvent: true);
            }

            return removed;
        }

        public async Task<bool> UpdateBoardColumnAsync(int projectId, int boardColumnId, string name, int? wipLimit, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            var updated = await _session.CreateProjectCommandService(dbContext).UpdateBoardColumnAsync(projectId, boardColumnId, name, wipLimit, cancellationToken);
            if (updated && _session.ActiveProject?.Id == projectId)
            {
                await _session.RefreshActiveProjectAsync(cancellationToken, raiseEvent: true);
            }

            return updated;
        }
    }

    public sealed class BoardQueryOperations
    {
        private readonly AppSession _session;

        internal BoardQueryOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<BoardColumnDto>> GetBoardAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateBoardQueryService(dbContext).GetBoardAsync(projectId, cancellationToken);
        }

        public async Task<IReadOnlyList<BoardColumnDto>> GetBoardAsync(int projectId, int? sprintId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateBoardQueryService(dbContext).GetBoardAsync(projectId, sprintId, cancellationToken);
        }
    }

    public sealed class UserQueryOperations
    {
        private readonly AppSession _session;

        internal UserQueryOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<User>> GetProjectUsersAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateUserQueryService(dbContext).GetProjectUsersAsync(projectId, cancellationToken);
        }
    }

    public sealed class UserCommandOperations
    {
        private readonly AppSession _session;

        internal UserCommandOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateUserCommandService(dbContext).GetAllAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateUserCommandService(dbContext).GetRolesAsync(cancellationToken);
        }

        public async Task<User> CreateAsync(int projectId, string userName, string displayName, string email, string password, ProjectRole projectRole, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateUserCommandService(dbContext).CreateAsync(projectId, userName, displayName, email, password, projectRole, roleNames, cancellationToken);
        }

        public async Task<User?> UpdateAsync(int userId, string displayName, string email, bool isActive, ProjectRole? projectRole, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateUserCommandService(dbContext).UpdateAsync(userId, displayName, email, isActive, projectRole, roleNames, cancellationToken);
        }

        public async Task<bool> ResetPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateUserCommandService(dbContext).ResetPasswordAsync(userId, newPassword, cancellationToken);
        }

        public async Task<User?> DeactivateAsync(int userId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateUserCommandService(dbContext).DeactivateAsync(userId, cancellationToken);
        }

        public async Task<User?> ActivateAsync(int userId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateUserCommandService(dbContext).ActivateAsync(userId, cancellationToken);
        }
    }

    public sealed class IssueOperations
    {
        private readonly AppSession _session;

        internal IssueOperations(AppSession session) => _session = session;

        public async Task<Issue> CreateAsync(IssueEditModel model, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateIssueService(dbContext).CreateAsync(model, cancellationToken);
        }

        public async Task<Issue?> UpdateAsync(IssueEditModel model, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateIssueService(dbContext).UpdateAsync(model, cancellationToken);
        }

        public async Task<bool> MoveAsync(int issueId, int targetStatusId, decimal boardPosition, int userId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateIssueService(dbContext).MoveAsync(issueId, targetStatusId, boardPosition, userId, cancellationToken);
        }

        public async Task<IssueDetailsDto?> GetDetailsAsync(int issueId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateIssueService(dbContext).GetDetailsAsync(issueId, cancellationToken);
        }

        public async Task<bool> DeleteAsync(int issueId, int? userId = null, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateIssueService(dbContext).DeleteAsync(issueId, userId, cancellationToken);
        }

        public async Task<IReadOnlyList<Issue>> GetPotentialParentsAsync(int projectId, IssueType childType, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await new IssueRepository(dbContext).GetPotentialParentsAsync(projectId, childType, cancellationToken);
        }

        public async Task<IReadOnlyList<Issue>> GetSubIssuesAsync(int issueId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await new IssueRepository(dbContext).GetSubIssuesAsync(issueId, cancellationToken);
        }
    }

    public sealed class JqlOperations
    {
        private readonly AppSession _session;

        internal JqlOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<IssueDto>> ExecuteQueryAsync(string? jql, int projectId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateJqlService(dbContext).ExecuteQueryAsync(jql, projectId, cancellationToken);
        }

        public JqlQuery Parse(string? jql)
        {
            using var dbContext = _session.CreateDbContext();
            return _session.CreateJqlService(dbContext).Parse(jql);
        }
    }

    public sealed class SavedFilterOperations
    {
        private readonly AppSession _session;

        internal SavedFilterOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<SavedFilterDto>> GetByProjectAsync(int projectId, int userId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateSavedFilterService(dbContext).GetByProjectAsync(projectId, userId, cancellationToken);
        }

        public async Task<SavedFilterDto> CreateAsync(int projectId, int userId, string name, string queryText, bool isFavorite = false, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateSavedFilterService(dbContext).CreateAsync(projectId, userId, name, queryText, isFavorite, cancellationToken);
        }

        public async Task<SavedFilterDto?> UpdateAsync(int savedFilterId, int userId, string name, string queryText, bool isFavorite = false, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateSavedFilterService(dbContext).UpdateAsync(savedFilterId, userId, name, queryText, isFavorite, cancellationToken);
        }

        public async Task<bool> DeleteAsync(int savedFilterId, int userId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateSavedFilterService(dbContext).DeleteAsync(savedFilterId, userId, cancellationToken);
        }
    }

    public sealed class LabelOperations
    {
        private readonly AppSession _session;

        internal LabelOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<ProjectLabel>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateLabelService(dbContext).GetByProjectAsync(projectId, cancellationToken);
        }

        public async Task<ProjectLabel> CreateAsync(int projectId, string name, string color, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateLabelService(dbContext).CreateAsync(projectId, name, color, cancellationToken);
        }

        public async Task<ProjectLabel?> UpdateAsync(int labelId, string name, string color, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateLabelService(dbContext).UpdateAsync(labelId, name, color, cancellationToken);
        }

        public async Task<bool> DeleteAsync(int labelId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateLabelService(dbContext).DeleteAsync(labelId, cancellationToken);
        }

        public async Task<bool> AssignToIssueAsync(int issueId, IReadOnlyCollection<int> labelIds, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateLabelService(dbContext).AssignToIssueAsync(issueId, labelIds, cancellationToken);
        }
    }

    public sealed class ComponentOperations
    {
        private readonly AppSession _session;

        internal ComponentOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<ProjectComponent>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateComponentService(dbContext).GetByProjectAsync(projectId, cancellationToken);
        }

        public async Task<ProjectComponent> CreateAsync(int projectId, string name, string? description, int? leadUserId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateComponentService(dbContext).CreateAsync(projectId, name, description, leadUserId, cancellationToken);
        }

        public async Task<ProjectComponent?> UpdateAsync(int componentId, string name, string? description, int? leadUserId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateComponentService(dbContext).UpdateAsync(componentId, name, description, leadUserId, cancellationToken);
        }

        public async Task<bool> DeleteAsync(int componentId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateComponentService(dbContext).DeleteAsync(componentId, cancellationToken);
        }

        public async Task<bool> AssignToIssueAsync(int issueId, int? componentId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateComponentService(dbContext).AssignToIssueAsync(issueId, componentId, cancellationToken);
        }
    }

    public sealed class VersionOperations
    {
        private readonly AppSession _session;

        internal VersionOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<ProjectVersionEntity>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateVersionService(dbContext).GetByProjectAsync(projectId, cancellationToken);
        }

        public async Task<ProjectVersionEntity> CreateAsync(int projectId, string name, string? description, DateTime? releaseDate, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateVersionService(dbContext).CreateAsync(projectId, name, description, releaseDate, cancellationToken);
        }

        public async Task<ProjectVersionEntity?> UpdateAsync(int versionId, string name, string? description, DateTime? releaseDate, bool isReleased, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateVersionService(dbContext).UpdateAsync(versionId, name, description, releaseDate, isReleased, cancellationToken);
        }

        public async Task<bool> DeleteAsync(int versionId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateVersionService(dbContext).DeleteAsync(versionId, cancellationToken);
        }

        public async Task<bool> AssignToIssueAsync(int issueId, int? versionId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateVersionService(dbContext).AssignToIssueAsync(issueId, versionId, cancellationToken);
        }

        public async Task<ProjectVersionEntity?> MarkReleasedAsync(int versionId, DateTime? releaseDate = null, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateVersionService(dbContext).MarkReleasedAsync(versionId, releaseDate, cancellationToken);
        }
    }


    public sealed class WorkflowOperations
    {
        private readonly AppSession _session;

        internal WorkflowOperations(AppSession session) => _session = session;

        public async Task<WorkflowDefinitionDto?> GetDefaultWorkflowAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateWorkflowService(dbContext).GetDefaultWorkflowAsync(projectId, cancellationToken);
        }

        public async Task<IReadOnlyList<WorkflowStatusOptionDto>> GetAllowedTransitionsAsync(int issueId, int userId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateWorkflowService(dbContext).GetAllowedTransitionsAsync(issueId, userId, cancellationToken);
        }

        public async Task<WorkflowTransitionResult> ExecuteTransitionAsync(int issueId, int toStatusId, int userId, decimal? boardPosition = null, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateWorkflowService(dbContext).ExecuteTransitionAsync(issueId, toStatusId, userId, boardPosition, cancellationToken);
        }

        public async Task<WorkflowStatus> CreateStatusAsync(int projectId, string name, string color, StatusCategory category, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateWorkflowService(dbContext).CreateStatusAsync(projectId, name, color, category, cancellationToken);
        }

        public async Task<WorkflowStatus?> UpdateStatusAsync(int workflowStatusId, string name, string color, StatusCategory category, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateWorkflowService(dbContext).UpdateStatusAsync(workflowStatusId, name, color, category, cancellationToken);
        }

        public async Task<bool> DeleteStatusAsync(int workflowStatusId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateWorkflowService(dbContext).DeleteStatusAsync(workflowStatusId, cancellationToken);
        }

        public async Task<WorkflowTransition?> CreateTransitionAsync(int projectId, int fromStatusId, int toStatusId, string name, IReadOnlyCollection<string> allowedRoleNames, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateWorkflowService(dbContext).CreateTransitionAsync(projectId, fromStatusId, toStatusId, name, allowedRoleNames, cancellationToken);
        }

        public async Task<WorkflowTransition?> UpdateTransitionAsync(int transitionId, string name, IReadOnlyCollection<string> allowedRoleNames, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateWorkflowService(dbContext).UpdateTransitionAsync(transitionId, name, allowedRoleNames, cancellationToken);
        }

        public async Task<bool> DeleteTransitionAsync(int transitionId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateWorkflowService(dbContext).DeleteTransitionAsync(transitionId, cancellationToken);
        }
    }
    public sealed class CommentOperations
    {
        private readonly AppSession _session;

        internal CommentOperations(AppSession session) => _session = session;

        public async Task<Comment> AddAsync(int issueId, int userId, int projectId, string body, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateCommentService(dbContext).AddAsync(issueId, userId, projectId, body, cancellationToken);
        }

        public async Task<Comment?> UpdateAsync(int commentId, int userId, string body, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateCommentService(dbContext).UpdateAsync(commentId, userId, body, cancellationToken);
        }

        public async Task<bool> SoftDeleteAsync(int commentId, int userId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateCommentService(dbContext).SoftDeleteAsync(commentId, userId, cancellationToken);
        }
    }

    public sealed class SprintOperations
    {
        private readonly AppSession _session;

        internal SprintOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<Sprint>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateSprintService(dbContext).GetByProjectAsync(projectId, cancellationToken);
        }

        public async Task<Sprint?> GetActiveByProjectAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateSprintService(dbContext).GetActiveByProjectAsync(projectId, cancellationToken);
        }

        public async Task<IReadOnlyList<Issue>> GetAssignableIssuesAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateSprintService(dbContext).GetAssignableIssuesAsync(projectId, cancellationToken);
        }

        public async Task<Sprint> CreateAsync(int projectId, string name, string? goal, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateSprintService(dbContext).CreateAsync(projectId, name, goal, startDate, endDate, cancellationToken);
        }

        public async Task<bool> AssignIssuesAsync(int sprintId, IReadOnlyCollection<int> issueIds, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateSprintService(dbContext).AssignIssuesAsync(sprintId, issueIds, cancellationToken);
        }

        public async Task<bool> StartSprintAsync(int sprintId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateSprintService(dbContext).StartSprintAsync(sprintId, cancellationToken);
        }

        public async Task<bool> CloseSprintAsync(int sprintId, int? moveToSprintId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateSprintService(dbContext).CloseSprintAsync(sprintId, moveToSprintId, cancellationToken);
        }

        public async Task<BurndownReportDto?> GetBurndownDataAsync(int sprintId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateSprintService(dbContext).GetBurndownDataAsync(sprintId, cancellationToken);
        }

        public async Task<VelocityReportDto> GetVelocityDataAsync(int projectId, int lastN = 6, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateSprintService(dbContext).GetVelocityDataAsync(projectId, lastN, cancellationToken);
        }
    }

    public sealed class ActivityLogOperations
    {
        private readonly AppSession _session;

        internal ActivityLogOperations(AppSession session) => _session = session;

        public async Task<IReadOnlyList<JiraClone.Domain.Entities.ActivityLog>> GetIssueActivityAsync(int issueId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateActivityLogService(dbContext).GetIssueActivityAsync(issueId, cancellationToken);
        }
    }

    public sealed class AttachmentOperations
    {
        private readonly AppSession _session;

        internal AttachmentOperations(AppSession session) => _session = session;

        public async Task<Attachment> AddAsync(int issueId, int projectId, int userId, string sourceFilePath, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateAttachmentFacade(dbContext).AddAsync(issueId, projectId, userId, sourceFilePath, cancellationToken);
        }

        public async Task<IReadOnlyList<Attachment>> GetByIssueAsync(int issueId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateAttachmentFacade(dbContext).GetByIssueAsync(issueId, cancellationToken);
        }

        public async Task<string?> ResolveDownloadPathAsync(int attachmentId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateAttachmentFacade(dbContext).ResolveDownloadPathAsync(attachmentId, cancellationToken);
        }

        public async Task<bool> SoftDeleteAsync(int attachmentId, int userId, int projectId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateAttachmentFacade(dbContext).SoftDeleteAsync(attachmentId, userId, projectId, cancellationToken);
        }
    }
}



















