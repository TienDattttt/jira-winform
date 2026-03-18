using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Seed;

public static class SeedData
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        var createdAt = new DateTime(2026, 03, 10, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Admin", Description = "Full system access", CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt },
            new Role { Id = 2, Name = "ProjectManager", Description = "Project administration access", CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt },
            new Role { Id = 3, Name = "Developer", Description = "Issue editing access", CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt },
            new Role { Id = 4, Name = "Viewer", Description = "Read-only access", CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt });

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                UserName = "admin",
                DisplayName = "Admin User",
                Email = "admin@jiraclone.local",
                PasswordHash = "kgu+8rnxTM1wAdC5cmxrD58zMWHiBN9ocgia5jcEKlU=",
                PasswordSalt = "E8M6vZg6o0mM1k3m8Xx3MQ==",
                IsActive = true,
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = createdAt
            },
            new User
            {
                Id = 2,
                UserName = "gaben",
                DisplayName = "Gaben",
                Email = "gaben@jiraclone.local",
                PasswordHash = "kgu+8rnxTM1wAdC5cmxrD58zMWHiBN9ocgia5jcEKlU=",
                PasswordSalt = "E8M6vZg6o0mM1k3m8Xx3MQ==",
                IsActive = true,
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = createdAt
            },
            new User
            {
                Id = 3,
                UserName = "yoda",
                DisplayName = "Yoda",
                Email = "yoda@jiraclone.local",
                PasswordHash = "kgu+8rnxTM1wAdC5cmxrD58zMWHiBN9ocgia5jcEKlU=",
                PasswordSalt = "E8M6vZg6o0mM1k3m8Xx3MQ==",
                IsActive = true,
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = createdAt
            });

        modelBuilder.Entity<UserRole>().HasData(
            new { UserId = 1, RoleId = 1 },
            new { UserId = 2, RoleId = 3 },
            new { UserId = 3, RoleId = 2 });

        modelBuilder.Entity<Project>().HasData(new Project
        {
            Id = 1,
            Key = "JIRA",
            Name = "Jira Clone Migration",
            Description = "WinForms migration workspace",
            Category = ProjectCategory.Software,
            Url = "https://localhost/jira-clone",
            IsActive = true,
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt
        });

        modelBuilder.Entity<ProjectMember>().HasData(
            new { ProjectId = 1, UserId = 1, ProjectRole = ProjectRole.Admin, JoinedAtUtc = createdAt },
            new { ProjectId = 1, UserId = 2, ProjectRole = ProjectRole.Developer, JoinedAtUtc = createdAt },
            new { ProjectId = 1, UserId = 3, ProjectRole = ProjectRole.ProjectManager, JoinedAtUtc = createdAt });

        modelBuilder.Entity<WorkflowDefinition>().HasData(new WorkflowDefinition
        {
            Id = 1,
            ProjectId = 1,
            Name = "Default Workflow",
            IsDefault = true,
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt
        });

        modelBuilder.Entity<WorkflowStatus>().HasData(
            new WorkflowStatus { Id = 1, WorkflowDefinitionId = 1, Name = "Backlog", Color = "#42526E", Category = StatusCategory.ToDo, DisplayOrder = 1, CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt },
            new WorkflowStatus { Id = 2, WorkflowDefinitionId = 1, Name = "Selected", Color = "#4C9AFF", Category = StatusCategory.ToDo, DisplayOrder = 2, CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt },
            new WorkflowStatus { Id = 3, WorkflowDefinitionId = 1, Name = "In Progress", Color = "#0052CC", Category = StatusCategory.InProgress, DisplayOrder = 3, CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt },
            new WorkflowStatus { Id = 4, WorkflowDefinitionId = 1, Name = "Done", Color = "#36B37E", Category = StatusCategory.Done, DisplayOrder = 4, CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt });

        var transitions = new List<object>();
        var transitionRoles = new List<object>();
        var transitionId = 1;
        var statusIds = new[] { 1, 2, 3, 4 };
        foreach (var fromStatusId in statusIds)
        {
            foreach (var toStatusId in statusIds.Where(x => x != fromStatusId))
            {
                transitions.Add(new
                {
                    Id = transitionId,
                    WorkflowDefinitionId = 1,
                    FromStatusId = fromStatusId,
                    ToStatusId = toStatusId,
                    Name = $"{ResolveStatusName(fromStatusId)} to {ResolveStatusName(toStatusId)}",
                    CreatedAtUtc = createdAt,
                    UpdatedAtUtc = createdAt
                });

                transitionRoles.Add(new { WorkflowTransitionId = transitionId, RoleId = 1 });
                transitionRoles.Add(new { WorkflowTransitionId = transitionId, RoleId = 2 });
                transitionRoles.Add(new { WorkflowTransitionId = transitionId, RoleId = 3 });
                transitionId++;
            }
        }

        modelBuilder.Entity<WorkflowTransition>().HasData(transitions.ToArray());
        modelBuilder.Entity("WorkflowTransitionRole").HasData(transitionRoles.ToArray());

        modelBuilder.Entity<BoardColumn>().HasData(
            new { Id = 1, ProjectId = 1, Name = "Backlog", WorkflowStatusId = 1, DisplayOrder = 1, WipLimit = (int?)null, CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt },
            new { Id = 2, ProjectId = 1, Name = "Selected", WorkflowStatusId = 2, DisplayOrder = 2, WipLimit = (int?)null, CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt },
            new { Id = 3, ProjectId = 1, Name = "In Progress", WorkflowStatusId = 3, DisplayOrder = 3, WipLimit = (int?)null, CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt },
            new { Id = 4, ProjectId = 1, Name = "Done", WorkflowStatusId = 4, DisplayOrder = 4, WipLimit = (int?)null, CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt });

        modelBuilder.Entity<Label>().HasData(
            new Label { Id = 1, ProjectId = 1, Name = "Desktop", Color = "#4688EC", CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt },
            new Label { Id = 2, ProjectId = 1, Name = "Migration", Color = "#2ABB7F", CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt },
            new Label { Id = 3, ProjectId = 1, Name = "Board", Color = "#FCA700", CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt });

        modelBuilder.Entity<Component>().HasData(
            new Component { Id = 1, ProjectId = 1, Name = "Board UI", Description = "Column layout, issue cards, and interaction polish.", LeadUserId = 2, CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt },
            new Component { Id = 2, ProjectId = 1, Name = "Persistence", Description = "Entity Framework mappings and repository behavior.", LeadUserId = 3, CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt });

        modelBuilder.Entity<ProjectVersion>().HasData(
            new ProjectVersion { Id = 1, ProjectId = 1, Name = "Desktop MVP", Description = "First usable internal desktop milestone.", ReleaseDate = new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc), IsReleased = false, CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt },
            new ProjectVersion { Id = 2, ProjectId = 1, Name = "Alpha Cut", Description = "Initial feature-complete alpha cut.", ReleaseDate = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc), IsReleased = true, CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt });

        modelBuilder.Entity<Sprint>().HasData(new
        {
            Id = 1,
            ProjectId = 1,
            Name = "Sprint 1",
            Goal = "Seed the desktop migration baseline",
            StartDate = new DateOnly(2026, 3, 10),
            EndDate = new DateOnly(2026, 3, 24),
            State = SprintState.Active,
            ClosedAtUtc = (DateTime?)null,
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt
        });

        modelBuilder.Entity<Issue>().HasData(
            new
            {
                Id = 1,
                ProjectId = 1,
                SprintId = 1,
                FixVersionId = (int?)1,
                IssueKey = "JIRA-1",
                Title = "Set up WinForms solution skeleton",
                DescriptionHtml = "Build the initial desktop solution.",
                DescriptionText = "Build the initial desktop solution.",
                Type = IssueType.Story,
                WorkflowStatusId = 1,
                Priority = IssuePriority.High,
                ReporterId = 3,
                CreatedById = 1,
                EstimateHours = 8,
                TimeSpentHours = 2,
                TimeRemainingHours = 6,
                StoryPoints = 5,
                DueDate = new DateOnly(2026, 3, 20),
                BoardPosition = 1m,
                IsDeleted = false,
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = createdAt
            },
            new
            {
                Id = 2,
                ProjectId = 1,
                SprintId = 1,
                FixVersionId = (int?)2,
                IssueKey = "JIRA-2",
                Title = "Implement SQL Server persistence model",
                DescriptionHtml = "Add EF Core context and mappings.",
                DescriptionText = "Add EF Core context and mappings.",
                Type = IssueType.Task,
                WorkflowStatusId = 2,
                Priority = IssuePriority.High,
                ReporterId = 1,
                CreatedById = 1,
                EstimateHours = 10,
                TimeSpentHours = 1,
                TimeRemainingHours = 9,
                StoryPoints = 3,
                DueDate = new DateOnly(2026, 3, 22),
                BoardPosition = 1m,
                IsDeleted = false,
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = createdAt
            },
            new
            {
                Id = 3,
                ProjectId = 1,
                SprintId = 1,
                FixVersionId = (int?)1,
                IssueKey = "JIRA-3",
                Title = "Build board screen",
                DescriptionHtml = "Render issue columns in the desktop app.",
                DescriptionText = "Render issue columns in the desktop app.",
                Type = IssueType.Story,
                WorkflowStatusId = 3,
                Priority = IssuePriority.Medium,
                ReporterId = 1,
                CreatedById = 2,
                EstimateHours = 16,
                TimeSpentHours = 6,
                TimeRemainingHours = 10,
                StoryPoints = 8,
                DueDate = new DateOnly(2026, 3, 25),
                BoardPosition = 1m,
                IsDeleted = false,
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = createdAt
            },
            new
            {
                Id = 4,
                ProjectId = 1,
                SprintId = 1,
                FixVersionId = (int?)2,
                IssueKey = "JIRA-4",
                Title = "Seed admin login",
                DescriptionHtml = "Provide an initial admin credential for local login.",
                DescriptionText = "Provide an initial admin credential for local login.",
                Type = IssueType.Task,
                WorkflowStatusId = 4,
                Priority = IssuePriority.Low,
                ReporterId = 3,
                CreatedById = 1,
                EstimateHours = 2,
                TimeSpentHours = 2,
                TimeRemainingHours = 0,
                StoryPoints = 1,
                DueDate = new DateOnly(2026, 3, 12),
                BoardPosition = 1m,
                IsDeleted = false,
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = createdAt
            });

        modelBuilder.Entity<IssueAssignee>().HasData(
            new { IssueId = 1, UserId = 2, AssignedAtUtc = createdAt },
            new { IssueId = 2, UserId = 3, AssignedAtUtc = createdAt },
            new { IssueId = 3, UserId = 2, AssignedAtUtc = createdAt },
            new { IssueId = 3, UserId = 3, AssignedAtUtc = createdAt });

        modelBuilder.Entity<IssueLabel>().HasData(
            new { IssueId = 1, LabelId = 1 },
            new { IssueId = 1, LabelId = 2 },
            new { IssueId = 2, LabelId = 2 },
            new { IssueId = 3, LabelId = 1 },
            new { IssueId = 3, LabelId = 3 });

        modelBuilder.Entity<IssueComponent>().HasData(
            new { IssueId = 2, ComponentId = 2 },
            new { IssueId = 3, ComponentId = 1 });

        modelBuilder.Entity<Comment>().HasData(
            new { Id = 1, IssueId = 3, UserId = 3, Body = "Board rendering is in progress.", IsDeleted = false, CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt },
            new { Id = 2, IssueId = 2, UserId = 1, Body = "Schema should mirror the migration plan.", IsDeleted = false, CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt });

        modelBuilder.Entity<ActivityLog>().HasData(
            new { Id = 1, ProjectId = 1, IssueId = 1, UserId = 1, ActionType = ActivityActionType.Created, FieldName = (string?)null, OldValue = (string?)null, NewValue = "Set up WinForms solution skeleton", OccurredAtUtc = createdAt, MetadataJson = (string?)null, CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt },
            new { Id = 2, ProjectId = 1, IssueId = 3, UserId = 2, ActionType = ActivityActionType.StatusChanged, FieldName = nameof(Issue.WorkflowStatusId), OldValue = "Selected", NewValue = "In Progress", OccurredAtUtc = createdAt, MetadataJson = "{\"OldStatusId\":2,\"OldStatusName\":\"Selected\",\"OldCategory\":1,\"NewStatusId\":3,\"NewStatusName\":\"In Progress\",\"NewCategory\":2}", CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt });
    }

    private static string ResolveStatusName(int statusId) => statusId switch
    {
        1 => "Backlog",
        2 => "Selected",
        3 => "In Progress",
        4 => "Done",
        _ => "Status"
    };
}
