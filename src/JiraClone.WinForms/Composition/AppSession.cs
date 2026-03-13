using JiraClone.Application.ActivityLog;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Attachments;
using JiraClone.Application.Auth;
using JiraClone.Application.Boards;
using JiraClone.Application.Comments;
using JiraClone.Application.Issues;
using JiraClone.Application.Models;
using JiraClone.Application.Projects;
using JiraClone.Application.Roles;
using JiraClone.Application.Sprints;
using JiraClone.Application.Users;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.Infrastructure.Security;
using JiraClone.Infrastructure.Storage;
using JiraClone.Persistence;
using JiraClone.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.WinForms.Composition;

public sealed class AppSession : IDisposable
{
    private readonly IDbContextFactory<JiraCloneDbContext> _dbContextFactory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly FileShareAttachmentService _attachmentStorage;

    public AppSession(IDbContextFactory<JiraCloneDbContext> dbContextFactory, string attachmentRootPath)
    {
        _dbContextFactory = dbContextFactory;
        _passwordHasher = new Sha256PasswordHasher();
        _attachmentStorage = new FileShareAttachmentService(attachmentRootPath);

        using var migrationContext = _dbContextFactory.CreateDbContext();
        migrationContext.Database.Migrate();

        CurrentUserContext = new CurrentUserContext();
        Authorization = new AuthorizationService(CurrentUserContext);
        Authentication = new AuthenticationOperations(this);
        Projects = new ProjectQueryOperations(this);
        ProjectCommands = new ProjectCommandOperations(this);
        Board = new BoardQueryOperations(this);
        Users = new UserQueryOperations(this);
        UserCommands = new UserCommandOperations(this);
        Issues = new IssueOperations(this);
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
    public CommentOperations Comments { get; }
    public SprintOperations Sprints { get; }
    public ActivityLogOperations ActivityLog { get; }
    public AttachmentOperations Attachments { get; }

    internal JiraCloneDbContext CreateDbContext() => _dbContextFactory.CreateDbContext();

    internal AuthenticationService CreateAuthenticationService(JiraCloneDbContext dbContext) =>
        new(new UserRepository(dbContext), _passwordHasher, CurrentUserContext);

    internal ProjectQueryService CreateProjectQueryService(JiraCloneDbContext dbContext) =>
        new(new ProjectRepository(dbContext));

    internal ProjectCommandService CreateProjectCommandService(JiraCloneDbContext dbContext) =>
        new(new ProjectRepository(dbContext), new UserRepository(dbContext), Authorization, new UnitOfWork(dbContext));

    internal BoardQueryService CreateBoardQueryService(JiraCloneDbContext dbContext) =>
        new(new IssueRepository(dbContext));

    internal UserQueryService CreateUserQueryService(JiraCloneDbContext dbContext) =>
        new(new UserRepository(dbContext));

    internal UserCommandService CreateUserCommandService(JiraCloneDbContext dbContext) =>
        new(new UserRepository(dbContext), new ProjectRepository(dbContext), _passwordHasher, Authorization, new UnitOfWork(dbContext));

    internal IssueService CreateIssueService(JiraCloneDbContext dbContext) =>
        new(
            new IssueRepository(dbContext),
            new UserRepository(dbContext),
            new CommentRepository(dbContext),
            new AttachmentRepository(dbContext),
            Authorization,
            new ActivityLogRepository(dbContext),
            new UnitOfWork(dbContext));

    internal CommentService CreateCommentService(JiraCloneDbContext dbContext) =>
        new(new CommentRepository(dbContext), Authorization, new ActivityLogRepository(dbContext), new UnitOfWork(dbContext));

    internal SprintService CreateSprintService(JiraCloneDbContext dbContext) =>
        new(new SprintRepository(dbContext), new IssueRepository(dbContext), Authorization, new UnitOfWork(dbContext));

    internal ActivityLogService CreateActivityLogService(JiraCloneDbContext dbContext) =>
        new(new ActivityLogRepository(dbContext));

    internal AttachmentFacade CreateAttachmentFacade(JiraCloneDbContext dbContext) =>
        new(_attachmentStorage, new AttachmentRepository(dbContext), Authorization, new ActivityLogRepository(dbContext), new UnitOfWork(dbContext));

    public Task RunSerializedAsync(Func<Task> operation, CancellationToken cancellationToken = default) => operation();

    public Task<T> RunSerializedAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default) => operation();

    public void Dispose()
    {
    }

    public sealed class AuthenticationOperations
    {
        private readonly AppSession _session;

        internal AuthenticationOperations(AppSession session) => _session = session;

        public async Task<AuthResult> LoginAsync(string userName, string password, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateAuthenticationService(dbContext).LoginAsync(userName, password, cancellationToken);
        }
    }

    public sealed class ProjectQueryOperations
    {
        private readonly AppSession _session;

        internal ProjectQueryOperations(AppSession session) => _session = session;

        public async Task<Project?> GetActiveProjectAsync(CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateProjectQueryService(dbContext).GetActiveProjectAsync(cancellationToken);
        }
    }

    public sealed class ProjectCommandOperations
    {
        private readonly AppSession _session;

        internal ProjectCommandOperations(AppSession session) => _session = session;

        public async Task<Project?> UpdateProjectAsync(int projectId, string name, string? description, ProjectCategory category, string? url, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateProjectCommandService(dbContext).UpdateProjectAsync(projectId, name, description, category, url, cancellationToken);
        }

        public async Task<bool> AddMemberAsync(int projectId, int userId, ProjectRole projectRole, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateProjectCommandService(dbContext).AddMemberAsync(projectId, userId, projectRole, cancellationToken);
        }

        public async Task<bool> UpdateMemberRoleAsync(int projectId, int userId, ProjectRole projectRole, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateProjectCommandService(dbContext).UpdateMemberRoleAsync(projectId, userId, projectRole, cancellationToken);
        }

        public async Task<bool> RemoveMemberAsync(int projectId, int userId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateProjectCommandService(dbContext).RemoveMemberAsync(projectId, userId, cancellationToken);
        }

        public async Task<bool> UpdateBoardColumnAsync(int projectId, int boardColumnId, string name, int? wipLimit, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateProjectCommandService(dbContext).UpdateBoardColumnAsync(projectId, boardColumnId, name, wipLimit, cancellationToken);
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

        public async Task<bool> MoveAsync(int issueId, IssueStatus targetStatus, decimal boardPosition, int userId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = _session.CreateDbContext();
            return await _session.CreateIssueService(dbContext).MoveAsync(issueId, targetStatus, boardPosition, userId, cancellationToken);
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
