using System.ComponentModel.DataAnnotations;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using JiraClone.Application.Projects;
using JiraClone.Application.Roles;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Moq;

namespace JiraClone.Tests.Application;

public class ProjectCommandServiceTests
{
    [Fact]
    public async Task CreateProjectAsync_ValidInput_CreatesProjectWithDefaultColumns()
    {
        var createdProject = default(Project);
        var projects = new Mock<IProjectRepository>();
        var users = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var currentUser = new User { Id = 99, UserName = "admin", DisplayName = "Admin User", Email = "admin@example.com", IsActive = true };

        projects.Setup(x => x.ExistsByKeyAsync("JCD", null, default)).ReturnsAsync(false);
        projects.Setup(x => x.AddAsync(It.IsAny<Project>(), default))
            .Callback<Project, CancellationToken>((project, _) =>
            {
                project.Id = 10;
                createdProject = project;
            })
            .Returns(Task.CompletedTask);
        projects.Setup(x => x.GetByIdAsync(10, default)).ReturnsAsync(() => createdProject!);
        users.Setup(x => x.GetByIdAsync(5, default)).ReturnsAsync(new User { Id = 5, UserName = "dev1", DisplayName = "Dev One", Email = "dev1@example.com", IsActive = true });
        users.Setup(x => x.GetRolesAsync(default)).ReturnsAsync([
            new Role { Id = 1, Name = RoleCatalog.Admin },
            new Role { Id = 2, Name = RoleCatalog.ProjectManager },
            new Role { Id = 3, Name = RoleCatalog.Developer },
            new Role { Id = 4, Name = RoleCatalog.Viewer }]);

        var service = CreateService(projects: projects, users: users, currentUser: currentUser, unitOfWork: unitOfWork);

        var project = await service.CreateProjectAsync(
            "Jira Clone Desktop",
            "jcd",
            ProjectCategory.Software,
            "Desktop delivery project",
            [new ProjectMemberInput(5, ProjectRole.Developer)]);

        Assert.NotNull(project);
        Assert.Equal(10, project.Id);
        Assert.Equal("JCD", createdProject!.Key);
        Assert.Equal("Jira Clone Desktop", createdProject.Name);
        Assert.Equal(4, createdProject.BoardColumns.Count);
        Assert.Contains(createdProject.Members, member => member.UserId == 99 && member.ProjectRole == ProjectRole.Admin);
        Assert.Contains(createdProject.Members, member => member.UserId == 5 && member.ProjectRole == ProjectRole.Developer);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateProjectAsync_DuplicateKey_ThrowsValidationException()
    {
        var projects = new Mock<IProjectRepository>();
        projects.Setup(x => x.ExistsByKeyAsync("JCD", null, default)).ReturnsAsync(true);
        var service = CreateService(projects: projects);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateProjectAsync(
            "Jira Clone Desktop",
            "JCD",
            ProjectCategory.Software,
            null,
            Array.Empty<ProjectMemberInput>()));
    }

    [Fact]
    public async Task UpdateProjectAsync_ValidChange_WritesActivityLog()
    {
        var project = new Project { Id = 1, Key = "PROJ", Name = "Old Name" };
        var projects = new Mock<IProjectRepository>();
        var activityLogs = new Mock<IActivityLogRepository>();
        projects.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(project);
        var service = CreateService(projects: projects, activityLogs: activityLogs);

        await service.UpdateProjectAsync(1, "New Name", null, ProjectCategory.Software, BoardType.Kanban, null);

        activityLogs.Verify(x => x.AddAsync(It.Is<ActivityLog>(log => log.ProjectId == 1 && log.FieldName == nameof(Project.Name) && log.NewValue == "New Name"), default), Times.Once);
        activityLogs.Verify(x => x.AddAsync(It.Is<ActivityLog>(log => log.ProjectId == 1 && log.FieldName == nameof(Project.BoardType) && log.NewValue == BoardType.Kanban.ToString()), default), Times.Once);
    }

    [Fact]
    public async Task ArchiveProjectAsync_ActiveProject_DisablesProjectAndSavesChanges()
    {
        var project = new Project { Id = 1, Key = "PROJ", Name = "Project", IsActive = true, Members = [new ProjectMember { ProjectId = 1, UserId = 99, ProjectRole = ProjectRole.Admin }] };
        var projects = new Mock<IProjectRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        projects.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(project);
        var service = CreateService(projects: projects, unitOfWork: unitOfWork);

        var archived = await service.ArchiveProjectAsync(1);

        Assert.True(archived);
        Assert.False(project.IsActive);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task DeleteProjectAsync_ActiveSprint_ThrowsValidationException()
    {
        var project = new Project { Id = 1, Key = "PROJ", Name = "Project", Members = [new ProjectMember { ProjectId = 1, UserId = 99, ProjectRole = ProjectRole.Admin }] };
        var projects = new Mock<IProjectRepository>();
        var sprints = new Mock<ISprintRepository>();
        projects.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(project);
        projects.Setup(x => x.GetDeleteSnapshotAsync(1, default)).ReturnsAsync(project);
        sprints.Setup(x => x.GetAllByProjectIdAsync(1, default)).ReturnsAsync([
            new Sprint { Id = 8, ProjectId = 1, Name = "Sprint 1", State = SprintState.Active }
        ]);

        var service = CreateService(projects: projects, sprints: sprints);

        await Assert.ThrowsAsync<ValidationException>(() => service.DeleteProjectAsync(1, 99));
    }

    [Fact]
    public async Task DeleteProjectAsync_ValidProject_SoftDeletesChildrenAndRemovesProject()
    {
        var project = new Project
        {
            Id = 1,
            Key = "PROJ",
            Name = "Project",
            Members = [new ProjectMember { ProjectId = 1, UserId = 99, ProjectRole = ProjectRole.Admin }],
            BoardColumns = [new BoardColumn { Id = 3, ProjectId = 1, Name = "Doing", WorkflowStatusId = 1 }],
            Labels = [new JiraClone.Domain.Entities.Label { Id = 4, ProjectId = 1, Name = "frontend", Color = "#0052CC" }],
            Components = [new JiraClone.Domain.Entities.Component { Id = 5, ProjectId = 1, Name = "UI" }],
            Versions = [new ProjectVersion { Id = 6, ProjectId = 1, Name = "1.0" }],
            WorkflowDefinitions = [new WorkflowDefinition { Id = 7, ProjectId = 1, Name = "Default", IsDefault = true }],
            SavedFilters = [new SavedFilter { Id = 9, ProjectId = 1, UserId = 99, Name = "Mine", QueryText = "assignee = currentUser()" }]
        };
        var issue = new Issue
        {
            Id = 2,
            ProjectId = 1,
            IssueKey = "PROJ-1",
            Title = "Implement delete",
            ReporterId = 99,
            CreatedById = 99,
            Comments = [new Comment { Id = 10, IssueId = 2, UserId = 99, Body = "Needs cleanup" }],
            Attachments = [new Attachment { Id = 11, IssueId = 2, UploadedById = 99, StoredFileName = "a.bin", OriginalFileName = "a.bin", ContentType = "application/octet-stream", FileExtension = ".bin", FileSizeBytes = 1, StoragePath = "storage\\a.bin", ChecksumSha256 = new string('a', 64) }]
        };
        issue.MoveTo(1, 1m);
        var sprint = new Sprint { Id = 12, ProjectId = 1, Name = "Sprint 1", State = SprintState.Closed };
        var projects = new Mock<IProjectRepository>();
        var issues = new Mock<IIssueRepository>();
        var sprints = new Mock<ISprintRepository>();
        var activityLogs = new Mock<IActivityLogRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        projects.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(project);
        projects.Setup(x => x.GetDeleteSnapshotAsync(1, default)).ReturnsAsync(project);
        projects.Setup(x => x.DeleteAsync(project, default)).Returns(Task.CompletedTask);
        issues.Setup(x => x.GetAllByProjectIdAsync(1, default)).ReturnsAsync([issue]);
        sprints.Setup(x => x.GetAllByProjectIdAsync(1, default)).ReturnsAsync([sprint]);

        var service = CreateService(projects: projects, issues: issues, sprints: sprints, activityLogs: activityLogs, unitOfWork: unitOfWork);

        var deleted = await service.DeleteProjectAsync(1, 99);

        Assert.True(deleted);
        Assert.True(issue.IsDeleted);
        Assert.True(issue.Comments.Single().IsDeleted);
        Assert.True(issue.Attachments.Single().IsDeleted);
        Assert.True(sprint.IsDeleted);
        activityLogs.Verify(x => x.AddAsync(It.Is<ActivityLog>(log => log.ProjectId == 1 && log.ActionType == ActivityActionType.Deleted && log.NewValue == "Project" && log.MetadataJson != null), default), Times.Once);
        projects.Verify(x => x.DeleteAsync(project, default), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Exactly(2));
    }

    [Fact]
    public async Task AddMemberAsync_NewMember_AddsMembershipAndSavesChanges()
    {
        var project = new Project { Id = 1, Key = "PROJ", Name = "Project", Members = [new ProjectMember { ProjectId = 1, UserId = 99, ProjectRole = ProjectRole.Admin }] };
        var user = new User { Id = 5, UserName = "dev1", DisplayName = "Dev One", Email = "dev1@example.com" };
        var projects = new Mock<IProjectRepository>();
        var users = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        projects.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(project);
        users.Setup(x => x.GetByIdAsync(5, default)).ReturnsAsync(user);
        var service = CreateService(projects: projects, users: users, unitOfWork: unitOfWork);

        var added = await service.AddMemberAsync(1, 5, ProjectRole.Developer);

        Assert.True(added);
        Assert.Equal(2, project.Members.Count);
        Assert.Contains(project.Members, member => member.UserId == 5 && member.ProjectRole == ProjectRole.Developer);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task AddMemberAsync_DuplicateMembership_ReturnsFalseWithoutSaving()
    {
        var project = new Project { Id = 1, Members = [new ProjectMember { ProjectId = 1, UserId = 5, ProjectRole = ProjectRole.Developer }] };
        var projects = new Mock<IProjectRepository>();
        var users = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        projects.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(project);
        users.Setup(x => x.GetByIdAsync(5, default)).ReturnsAsync(new User { Id = 5, UserName = "dev1", DisplayName = "Dev One", Email = "dev1@example.com" });
        var service = CreateService(projects: projects, users: users, unitOfWork: unitOfWork);

        var added = await service.AddMemberAsync(1, 5, ProjectRole.Developer);

        Assert.False(added);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task UpdateBoardColumnAsync_ValidInput_UpdatesColumnAndSavesChanges()
    {
        var column = new BoardColumn { Id = 7, ProjectId = 1, Name = "Doing", WipLimit = 2 };
        var project = new Project { Id = 1, BoardColumns = [column] };
        var projects = new Mock<IProjectRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        projects.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(project);
        var service = CreateService(projects: projects, unitOfWork: unitOfWork);

        var updated = await service.UpdateBoardColumnAsync(1, 7, "In Progress", 4);

        Assert.True(updated);
        Assert.Equal("In Progress", column.Name);
        Assert.Equal(4, column.WipLimit);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    private static ProjectCommandService CreateService(
        Mock<IProjectRepository>? projects = null,
        Mock<IUserRepository>? users = null,
        Mock<IIssueRepository>? issues = null,
        Mock<ISprintRepository>? sprints = null,
        Mock<IAuthorizationService>? authorization = null,
        Mock<IPermissionService>? permissionService = null,
        Mock<IActivityLogRepository>? activityLogs = null,
        User? currentUser = null,
        Mock<ICurrentUserContext>? currentUserContext = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        currentUserContext ??= new Mock<ICurrentUserContext>();
        currentUserContext.Setup(x => x.CurrentUser).Returns(currentUser ?? new User { Id = 99, UserName = "admin", DisplayName = "Admin User", Email = "admin@example.com", IsActive = true });
        currentUserContext.Setup(x => x.RequireUserId()).Returns((currentUser ?? new User { Id = 99, UserName = "admin", DisplayName = "Admin User", Email = "admin@example.com", IsActive = true }).Id);

        users ??= new Mock<IUserRepository>();
        users.Setup(x => x.GetRolesAsync(default)).ReturnsAsync([
            new Role { Id = 1, Name = RoleCatalog.Admin },
            new Role { Id = 2, Name = RoleCatalog.ProjectManager },
            new Role { Id = 3, Name = RoleCatalog.Developer },
            new Role { Id = 4, Name = RoleCatalog.Viewer }]);

        permissionService ??= new Mock<IPermissionService>();
        permissionService
            .Setup(x => x.HasPermissionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Permission>(), default))
            .ReturnsAsync(true);

        return new ProjectCommandService(
            (projects ?? new Mock<IProjectRepository>()).Object,
            users.Object,
            (issues ?? new Mock<IIssueRepository>()).Object,
            (sprints ?? new Mock<ISprintRepository>()).Object,
            (authorization ?? new Mock<IAuthorizationService>()).Object,
            permissionService.Object,
            (activityLogs ?? new Mock<IActivityLogRepository>()).Object,
            currentUserContext.Object,
            (unitOfWork ?? new Mock<IUnitOfWork>()).Object);
    }
}





