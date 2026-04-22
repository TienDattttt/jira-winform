using ClosedXML.Excel;
using ClosedXML.Graphics;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using JiraClone.Application.Reports;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace JiraClone.Tests.Application;

public class ExcelExportServiceTests
{
    [Fact]
    public async Task ExportProjectReportAsync_CreatesWorkbookWithRequiredSheetsAndStyles()
    {
        var project = CreateProject();
        var activeSprint = CreateSprint(11, SprintState.Active, "Sprint 11 - đối soát cửa hàng");
        var closedSprint = CreateSprint(10, SprintState.Closed, "Sprint 10 - ổn định vận hành", new DateOnly(2026, 4, 13), new DateOnly(2026, 4, 26));
        var issues = CreateIssues(activeSprint);

        var service = CreateService(
            project,
            [activeSprint, closedSprint],
            activeSprint,
            issues,
            CreateStatusActivities(issues[0].Id),
            CreateVelocityData(closedSprint),
            sprintReport: null);

        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

        try
        {
            await service.ExportProjectReportAsync(new ExcelReportExportRequest(project.Id, filePath));

            using var workbookHandle = OpenWorkbook(filePath);
            var workbook = workbookHandle.Workbook;
            Assert.Equal(4, workbook.Worksheets.Count);
            Assert.NotNull(workbook.Worksheet("Tổng quan"));
            Assert.NotNull(workbook.Worksheet("Sprint Report"));
            Assert.NotNull(workbook.Worksheet("Velocity"));
            Assert.NotNull(workbook.Worksheet("Tất cả Issues"));

            var sprintReportSheet = workbook.Worksheet("Sprint Report");
            Assert.Equal("Issue Key", sprintReportSheet.Cell("A1").GetString());
            Assert.Equal(XLColor.FromHtml("#1E3A5F").Color.ToArgb(), sprintReportSheet.Cell("A1").Style.Fill.BackgroundColor.Color.ToArgb());
            Assert.Equal(XLColor.White.Color.ToArgb(), sprintReportSheet.Cell("A1").Style.Font.FontColor.Color.ToArgb());
            Assert.True(sprintReportSheet.Cell("A1").Style.Font.Bold);

            var exportedIssueKeys = new[]
            {
                sprintReportSheet.Cell("A2").GetString(),
                sprintReportSheet.Cell("A3").GetString()
            };
            Assert.Contains("APR-101", exportedIssueKeys);
            Assert.Contains("APR-102", exportedIssueKeys);
            Assert.Equal(XLColor.White.Color.ToArgb(), sprintReportSheet.Cell("A2").Style.Fill.BackgroundColor.Color.ToArgb());
            Assert.Equal(XLColor.FromHtml("#F0F4F8").Color.ToArgb(), sprintReportSheet.Cell("A3").Style.Fill.BackgroundColor.Color.ToArgb());

            var summarySheet = workbook.Worksheet("Tổng quan");
            Assert.Equal("Tên project", summarySheet.Cell("A3").GetString());
            Assert.Equal(project.Name, summarySheet.Cell("B3").GetString());
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task ExportProjectReportAsync_WhenClosedSprintPreferred_UsesSprintReportDataAndCompletionDate()
    {
        var project = CreateProject();
        var activeSprint = CreateSprint(11, SprintState.Active, "Sprint 11 - đối soát cửa hàng");
        var closedSprint = CreateSprint(10, SprintState.Closed, "Sprint 10 - ổn định vận hành", new DateOnly(2026, 4, 13), new DateOnly(2026, 4, 26));
        var completedIssue = CreateIssue(101, "APR-101", "Ổn định đối soát COD", IssuePriority.High, StatusCategory.Done, closedSprint);
        completedIssue.CreatedAtUtc = new DateTime(2026, 4, 14, 1, 0, 0, DateTimeKind.Utc);
        completedIssue.UpdatedAtUtc = new DateTime(2026, 4, 23, 4, 0, 0, DateTimeKind.Utc);

        var sprintReport = new SprintReportDto(
            closedSprint.Id,
            closedSprint.Name,
            closedSprint.StartDate!.Value,
            closedSprint.EndDate!.Value,
            new DateTime(2026, 4, 26, 12, 0, 0, DateTimeKind.Utc),
            [new SprintReportIssueDto(completedIssue.Id, completedIssue.IssueKey, completedIssue.Title, completedIssue.Type, completedIssue.WorkflowStatus.Name, completedIssue.WorkflowStatus.Category, completedIssue.StoryPoints ?? 0, "Nguyen Minh Quan")],
            [],
            [],
            5,
            5,
            100d);

        var service = CreateService(
            project,
            [activeSprint, closedSprint],
            activeSprint,
            [completedIssue],
            CreateStatusActivities(completedIssue.Id),
            CreateVelocityData(closedSprint),
            sprintReport);

        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

        try
        {
            await service.ExportProjectReportAsync(new ExcelReportExportRequest(project.Id, filePath, closedSprint.Id));

            using var workbookHandle = OpenWorkbook(filePath);
            var workbook = workbookHandle.Workbook;
            var sprintReportSheet = workbook.Worksheet("Sprint Report");
            Assert.Equal(completedIssue.IssueKey, sprintReportSheet.Cell("A2").GetString());
            Assert.Equal("Cao", sprintReportSheet.Cell("D2").GetString());
            Assert.Equal("23/04/2026", sprintReportSheet.Cell("I2").GetFormattedString());
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    private static ExcelExportService CreateService(
        Project project,
        IReadOnlyList<Sprint> sprints,
        Sprint? activeSprint,
        IReadOnlyList<Issue> issues,
        IReadOnlyList<ActivityLog> statusActivities,
        VelocityReportDto velocityData,
        SprintReportDto? sprintReport)
    {
        var projectRepository = new Mock<IProjectRepository>();
        projectRepository.Setup(x => x.GetByIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetProjectIssuesAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(issues);

        var sprintService = new Mock<ISprintService>();
        sprintService.Setup(x => x.GetByProjectAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sprints);
        sprintService.Setup(x => x.GetActiveByProjectAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(activeSprint);
        sprintService.Setup(x => x.GetVelocityDataAsync(project.Id, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(velocityData);
        sprintService.Setup(x => x.GetSprintReportAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(sprintReport);

        var activityLogRepository = new Mock<IActivityLogRepository>();
        activityLogRepository.Setup(x => x.GetProjectStatusChangesAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(statusActivities);

        var permissionService = new Mock<IPermissionService>();
        permissionService.Setup(x => x.HasPermissionAsync(9, project.Id, Permission.ViewProject, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var currentUserContext = new Mock<ICurrentUserContext>();
        currentUserContext.SetupGet(x => x.CurrentUser).Returns(new User { Id = 9, UserName = "developer", DisplayName = "Developer" });
        currentUserContext.Setup(x => x.RequireUserId()).Returns(9);

        return new ExcelExportService(
            projectRepository.Object,
            issueRepository.Object,
            sprintService.Object,
            activityLogRepository.Object,
            permissionService.Object,
            currentUserContext.Object,
            NullLogger<ExcelExportService>.Instance);
    }

    private static Project CreateProject()
    {
        return new Project
        {
            Id = 1,
            Key = "APR",
            Name = "An Phuc Retail OMS",
            BoardType = BoardType.Scrum,
            Members =
            [
                new ProjectMember { ProjectId = 1, UserId = 9, ProjectRole = ProjectRole.Developer, User = new User { Id = 9, DisplayName = "Nguyen Minh Quan" } },
                new ProjectMember { ProjectId = 1, UserId = 12, ProjectRole = ProjectRole.ProjectManager, User = new User { Id = 12, DisplayName = "Pham Thu Lan" } }
            ]
        };
    }

    private static Sprint CreateSprint(
        int id,
        SprintState state,
        string name,
        DateOnly? startDate = null,
        DateOnly? endDate = null)
    {
        return new Sprint
        {
            Id = id,
            ProjectId = 1,
            Name = name,
            State = state,
            StartDate = startDate ?? new DateOnly(2026, 4, 27),
            EndDate = endDate ?? new DateOnly(2026, 5, 10),
            ClosedAtUtc = state == SprintState.Closed
                ? new DateTime(2026, 4, 26, 12, 0, 0, DateTimeKind.Utc)
                : null
        };
    }

    private static List<Issue> CreateIssues(Sprint sprint)
    {
        return
        [
            CreateIssue(101, "APR-101", "Đồng bộ đơn COD về kho", IssuePriority.High, StatusCategory.Done, sprint),
            CreateIssue(102, "APR-102", "Rà soát lệch tồn trung tâm", IssuePriority.Medium, StatusCategory.InProgress, sprint)
        ];
    }

    private static Issue CreateIssue(int id, string issueKey, string title, IssuePriority priority, StatusCategory category, Sprint sprint)
    {
        var workflow = new WorkflowDefinition { Id = 1, ProjectId = 1, Name = "Default", IsDefault = true };
        var status = new WorkflowStatus
        {
            Id = category == StatusCategory.Done ? 4 : 2,
            WorkflowDefinitionId = workflow.Id,
            WorkflowDefinition = workflow,
            Name = category == StatusCategory.Done ? "Done" : "In Progress",
            Category = category,
            Color = category == StatusCategory.Done ? "#36B37E" : "#0052CC",
            DisplayOrder = category == StatusCategory.Done ? 4 : 2
        };

        var issue = new Issue
        {
            Id = id,
            ProjectId = 1,
            IssueKey = issueKey,
            Title = title,
            Type = IssueType.Task,
            Priority = priority,
            ReporterId = 9,
            CreatedById = 9,
            SprintId = sprint.Id,
            Sprint = sprint,
            StoryPoints = priority == IssuePriority.High ? 5 : 3,
            WorkflowStatus = status,
            Assignees =
            [
                new IssueAssignee
                {
                    IssueId = id,
                    UserId = 9,
                    User = new User { Id = 9, DisplayName = "Nguyen Minh Quan" }
                }
            ],
            CreatedAtUtc = new DateTime(2026, 4, 21, 1, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 4, 22, 2, 0, 0, DateTimeKind.Utc)
        };
        issue.MoveTo(status.Id, 1m);
        return issue;
    }

    private static IReadOnlyList<ActivityLog> CreateStatusActivities(int issueId)
    {
        return
        [
            new ActivityLog
            {
                Id = 1,
                ProjectId = 1,
                IssueId = issueId,
                UserId = 9,
                ActionType = ActivityActionType.StatusChanged,
                OldValue = "In Progress",
                NewValue = "Done",
                OccurredAtUtc = new DateTime(2026, 4, 23, 2, 0, 0, DateTimeKind.Utc),
                MetadataJson = "{\"OldStatusId\":2,\"OldStatusName\":\"In Progress\",\"OldCategory\":1,\"NewStatusId\":4,\"NewStatusName\":\"Done\",\"NewCategory\":3}"
            }
        ];
    }

    private static VelocityReportDto CreateVelocityData(Sprint closedSprint)
    {
        return new VelocityReportDto(
            1,
            [new VelocitySprintDto(closedSprint.Id, closedSprint.Name, closedSprint.StartDate, closedSprint.EndDate, 13, 11)],
            11d);
    }

    private static WorkbookHandle OpenWorkbook(string filePath)
    {
        var fallbackFontPath = ResolveFallbackFontPath();
        if (string.IsNullOrWhiteSpace(fallbackFontPath))
        {
            return new WorkbookHandle(new XLWorkbook(filePath), fallbackFontStream: null);
        }

        var fallbackFontStream = File.OpenRead(fallbackFontPath);
        var loadOptions = new LoadOptions
        {
            GraphicEngine = DefaultGraphicEngine.CreateOnlyWithFonts(fallbackFontStream)
        };

        return new WorkbookHandle(new XLWorkbook(filePath, loadOptions), fallbackFontStream);
    }

    private static string? ResolveFallbackFontPath()
    {
        var fontDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (string.IsNullOrWhiteSpace(fontDirectory))
        {
            return null;
        }

        foreach (var fontFileName in new[] { "arial.ttf", "segoeui.ttf", "tahoma.ttf" })
        {
            var fullPath = Path.Combine(fontDirectory, fontFileName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private sealed class WorkbookHandle : IDisposable
    {
        private readonly Stream? _fallbackFontStream;

        public WorkbookHandle(XLWorkbook workbook, Stream? fallbackFontStream)
        {
            Workbook = workbook;
            _fallbackFontStream = fallbackFontStream;
        }

        public XLWorkbook Workbook { get; }

        public void Dispose()
        {
            Workbook.Dispose();
            _fallbackFontStream?.Dispose();
        }
    }
}
