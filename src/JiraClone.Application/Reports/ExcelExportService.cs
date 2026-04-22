using ClosedXML.Excel;
using ClosedXML.Graphics;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;

namespace JiraClone.Application.Reports;

public sealed class ExcelExportService : IExcelExportService
{
    private const string SummarySheetName = "Tổng quan";
    private const string SprintReportSheetName = "Sprint Report";
    private const string VelocitySheetName = "Velocity";
    private const string AllIssuesSheetName = "Tất cả Issues";
    private const string SummaryTitle = "BÁO CÁO TỔNG QUAN DỰ ÁN";
    private const string HeaderFillHex = "#1E3A5F";
    private const string ZebraFillHex = "#F0F4F8";
    private const string IntegerFormat = "0";
    private const string DateFormat = "dd/MM/yyyy";
    private const string PercentageFormat = "0.0%";
    private const string NoActiveSprintText = "Không có sprint active";
    private const string NoSprintIssuesText = "Không có issue cho sprint được chọn.";
    private const string NoVelocityDataText = "Chưa có sprint đã đóng để tính velocity.";
    private const string UnassignedText = "Chưa gán";
    private const string SprintPlaceholderText = "Không thuộc sprint";

    private static readonly string[] SummaryLabels =
    [
        "Tên project",
        "Project Key",
        "Board Type",
        "Sprint đang active",
        "Tổng số issues",
        "Số issues đã Done",
        "% hoàn thành",
        "Số thành viên",
        "Ngày xuất báo cáo"
    ];

    private static readonly string[] SprintReportHeaders =
    [
        "Issue Key",
        "Tiêu đề",
        "Loại",
        "Độ ưu tiên",
        "Trạng thái",
        "Assignee",
        "Story Points",
        "Ngày tạo",
        "Ngày hoàn thành"
    ];

    private static readonly string[] VelocityHeaders =
    [
        "Tên Sprint",
        "Ngày bắt đầu",
        "Ngày kết thúc",
        "Story Points committed",
        "Story Points completed",
        "Tỉ lệ hoàn thành (%)"
    ];

    private static readonly string[] AllIssueHeaders =
    [
        "Issue Key",
        "Tiêu đề",
        "Loại",
        "Độ ưu tiên",
        "Trạng thái",
        "Assignee",
        "Sprint",
        "Story Points",
        "Ngày tạo",
        "Ngày cập nhật"
    ];

    private readonly IProjectRepository _projects;
    private readonly IIssueRepository _issues;
    private readonly ISprintService _sprints;
    private readonly IActivityLogRepository _activityLogs;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<ExcelExportService> _logger;

    public ExcelExportService(
        IProjectRepository projects,
        IIssueRepository issues,
        ISprintService sprints,
        IActivityLogRepository activityLogs,
        IPermissionService permissionService,
        ICurrentUserContext currentUserContext,
        ILogger<ExcelExportService>? logger = null)
    {
        _projects = projects;
        _issues = issues;
        _sprints = sprints;
        _activityLogs = activityLogs;
        _permissionService = permissionService;
        _currentUserContext = currentUserContext;
        _logger = logger ?? NullLogger<ExcelExportService>.Instance;
    }

    public async Task ExportProjectReportAsync(ExcelReportExportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateRequest(request);
        await EnsurePermissionAsync(request.ProjectId, Permission.ViewProject, cancellationToken);

        try
        {
            var workbookData = await BuildWorkbookDataAsync(request, cancellationToken);
            EnsureTargetDirectoryExists(request.DestinationPath);

            using var workbookContext = CreateWorkbookContext();
            BuildSummarySheet(workbookContext.Workbook, workbookData);
            BuildSprintReportSheet(workbookContext.Workbook, workbookData);
            BuildVelocitySheet(workbookContext.Workbook, workbookData);
            BuildIssuesSheet(workbookContext.Workbook, workbookData);
            workbookContext.Workbook.SaveAs(request.DestinationPath);

            _logger.LogInformation(
                "Excel report exported for project {ProjectId} to {DestinationPath}.",
                request.ProjectId,
                request.DestinationPath);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to export Excel report for project {ProjectId} to {DestinationPath}.",
                request.ProjectId,
                request.DestinationPath);
            throw;
        }
    }

    private async Task<ExcelWorkbookData> BuildWorkbookDataAsync(ExcelReportExportRequest request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy dự án để xuất báo cáo.");

        var allIssues = (await _issues.GetProjectIssuesAsync(project.Id, cancellationToken))
            .OrderBy(issue => issue.WorkflowStatus.DisplayOrder)
            .ThenBy(issue => issue.IssueKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var allSprints = (await _sprints.GetByProjectAsync(project.Id, cancellationToken))
            .OrderByDescending(GetSprintSortDate)
            .ToList();
        var activeSprint = await _sprints.GetActiveByProjectAsync(project.Id, cancellationToken);
        var targetSprint = ResolveTargetSprint(allSprints, activeSprint, request.PreferredSprintId);
        var closedSprintCount = allSprints.Count(sprint => sprint.State == SprintState.Closed);
        var velocityData = await _sprints.GetVelocityDataAsync(project.Id, Math.Max(1, closedSprintCount), cancellationToken);
        var statusActivities = await _activityLogs.GetProjectStatusChangesAsync(project.Id, cancellationToken);

        var issuesById = allIssues.ToDictionary(issue => issue.Id);
        var statusActivitiesByIssueId = statusActivities
            .Where(activity => activity.IssueId.HasValue)
            .GroupBy(activity => activity.IssueId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ActivityLogEntity>)group.OrderBy(activity => activity.OccurredAtUtc).ThenBy(activity => activity.Id).ToList());

        var sprintReport = await BuildSprintReportDataAsync(
            project.Id,
            targetSprint,
            allIssues,
            issuesById,
            statusActivitiesByIssueId,
            cancellationToken);

        var doneIssueCount = allIssues.Count(issue => issue.WorkflowStatus.Category == StatusCategory.Done);
        var summary = new SummarySheetData(
            project.Name,
            project.Key,
            FormatBoardType(project.BoardType),
            FormatActiveSprint(activeSprint),
            allIssues.Count,
            doneIssueCount,
            allIssues.Count == 0 ? 0d : doneIssueCount / (double)allIssues.Count,
            project.Members.Count,
            GetTodayLocalDate());

        var velocityRows = velocityData.Sprints
            .Select(sprint => new VelocitySheetRow(
                sprint.SprintName,
                sprint.StartDate,
                sprint.EndDate,
                sprint.CommittedStoryPoints,
                sprint.CompletedStoryPoints,
                sprint.CommittedStoryPoints == 0
                    ? (sprint.CompletedStoryPoints == 0 ? 0d : 1d)
                    : sprint.CompletedStoryPoints / (double)sprint.CommittedStoryPoints))
            .ToList();

        var issueRows = allIssues
            .Select(issue => new AllIssueSheetRow(
                issue.IssueKey,
                issue.Title,
                FormatIssueType(issue.Type),
                FormatPriority(issue.Priority),
                issue.WorkflowStatus.Name,
                FormatAssignees(issue),
                issue.Sprint?.Name ?? SprintPlaceholderText,
                issue.StoryPoints ?? 0,
                ConvertUtcToLocalDate(issue.CreatedAtUtc),
                ConvertUtcToLocalDate(issue.UpdatedAtUtc)))
            .ToList();

        return new ExcelWorkbookData(summary, sprintReport, velocityRows, issueRows);
    }

    private async Task<SprintReportSheetData> BuildSprintReportDataAsync(
        int projectId,
        Sprint? targetSprint,
        IReadOnlyList<Issue> allIssues,
        IReadOnlyDictionary<int, Issue> issuesById,
        IReadOnlyDictionary<int, IReadOnlyList<ActivityLogEntity>> statusActivitiesByIssueId,
        CancellationToken cancellationToken)
    {
        _ = projectId;

        if (targetSprint is null)
        {
            return new SprintReportSheetData(null, []);
        }

        if (targetSprint.State == SprintState.Closed)
        {
            var sprintReport = await _sprints.GetSprintReportAsync(targetSprint.Id, cancellationToken);
            if (sprintReport is not null)
            {
                var closeDate = sprintReport.EndDate;
                var completedRows = sprintReport.CompletedWork
                    .Select(issue => MapClosedSprintIssue(issue, issuesById, statusActivitiesByIssueId, closeDate))
                    .ToList();
                var notCompletedRows = sprintReport.NotCompleted
                    .Select(issue => MapClosedSprintIssue(issue, issuesById, statusActivitiesByIssueId, closeDate))
                    .ToList();
                var removedRows = sprintReport.RemovedFromSprint
                    .Select(issue => MapClosedSprintIssue(issue, issuesById, statusActivitiesByIssueId, closeDate))
                    .ToList();

                return new SprintReportSheetData(targetSprint.Name, [.. completedRows, .. notCompletedRows, .. removedRows]);
            }
        }

        var sprintIssues = allIssues
            .Where(issue => issue.SprintId == targetSprint.Id)
            .OrderBy(issue => issue.WorkflowStatus.DisplayOrder)
            .ThenBy(issue => issue.IssueKey, StringComparer.OrdinalIgnoreCase)
            .Select(issue => new SprintReportSheetRow(
                issue.IssueKey,
                issue.Title,
                FormatIssueType(issue.Type),
                FormatPriority(issue.Priority),
                issue.WorkflowStatus.Name,
                FormatAssignees(issue),
                issue.StoryPoints ?? 0,
                ConvertUtcToLocalDate(issue.CreatedAtUtc),
                ResolveCompletionDate(
                    issue,
                    statusActivitiesByIssueId.GetValueOrDefault(issue.Id, Array.Empty<ActivityLogEntity>()),
                    cutoffDate: null)))
            .ToList();

        return new SprintReportSheetData(targetSprint.Name, sprintIssues);
    }

    private static SprintReportSheetRow MapClosedSprintIssue(
        SprintReportIssueDto sprintIssue,
        IReadOnlyDictionary<int, Issue> issuesById,
        IReadOnlyDictionary<int, IReadOnlyList<ActivityLogEntity>> statusActivitiesByIssueId,
        DateOnly closeDate)
    {
        if (!issuesById.TryGetValue(sprintIssue.IssueId, out var issue))
        {
            return new SprintReportSheetRow(
                sprintIssue.IssueKey,
                sprintIssue.Title,
                FormatIssueType(sprintIssue.Type),
                FormatPriority(IssuePriority.Medium),
                sprintIssue.StatusName,
                string.IsNullOrWhiteSpace(sprintIssue.AssigneeSummary) ? UnassignedText : sprintIssue.AssigneeSummary,
                sprintIssue.StoryPoints,
                null,
                null);
        }

        return new SprintReportSheetRow(
            sprintIssue.IssueKey,
            sprintIssue.Title,
            FormatIssueType(sprintIssue.Type),
            FormatPriority(issue.Priority),
            sprintIssue.StatusName,
            string.IsNullOrWhiteSpace(sprintIssue.AssigneeSummary) ? UnassignedText : sprintIssue.AssigneeSummary,
            sprintIssue.StoryPoints,
            ConvertUtcToLocalDate(issue.CreatedAtUtc),
            ResolveCompletionDate(
                issue,
                statusActivitiesByIssueId.GetValueOrDefault(issue.Id, Array.Empty<ActivityLogEntity>()),
                closeDate));
    }

    private void BuildSummarySheet(XLWorkbook workbook, ExcelWorkbookData data)
    {
        var worksheet = workbook.Worksheets.Add(SummarySheetName);
        worksheet.Cell("A1").Value = SummaryTitle;
        var titleRange = worksheet.Range("A1:B1");
        titleRange.Merge();
        titleRange.Style.Fill.BackgroundColor = XLColor.FromHtml(HeaderFillHex);
        titleRange.Style.Font.FontColor = XLColor.White;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 14;

        var values = new object?[]
        {
            data.Summary.ProjectName,
            data.Summary.ProjectKey,
            data.Summary.BoardType,
            data.Summary.ActiveSprint,
            data.Summary.TotalIssues,
            data.Summary.DoneIssues,
            data.Summary.CompletionRatio,
            data.Summary.MemberCount,
            data.Summary.ExportedOn.ToDateTime(TimeOnly.MinValue)
        };

        for (var index = 0; index < SummaryLabels.Length; index++)
        {
            var rowNumber = index + 3;
            worksheet.Cell(rowNumber, 1).Value = SummaryLabels[index];
            SetCellValue(worksheet.Cell(rowNumber, 2), values[index]);
        }

        var contentRange = worksheet.Range(3, 1, SummaryLabels.Length + 2, 2);
        contentRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        contentRange.Style.Font.FontSize = 11;
        worksheet.Range(3, 1, SummaryLabels.Length + 2, 1).Style.Font.Bold = true;
        ApplySummaryStriping(worksheet, 3, SummaryLabels.Length + 2);
        worksheet.Cell(7, 2).Style.NumberFormat.Format = IntegerFormat;
        worksheet.Cell(8, 2).Style.NumberFormat.Format = IntegerFormat;
        worksheet.Cell(9, 2).Style.NumberFormat.Format = PercentageFormat;
        worksheet.Cell(10, 2).Style.NumberFormat.Format = IntegerFormat;
        worksheet.Cell(11, 2).Style.NumberFormat.Format = DateFormat;
        worksheet.ColumnsUsed().AdjustToContents();
    }

    private void BuildSprintReportSheet(XLWorkbook workbook, ExcelWorkbookData data)
    {
        var worksheet = workbook.Worksheets.Add(SprintReportSheetName);
        BuildDataSheetHeader(worksheet, SprintReportHeaders);

        if (data.SprintReport.Rows.Count == 0)
        {
            worksheet.Cell(3, 1).Value = data.SprintReport.SprintName is null
                ? NoSprintIssuesText
                : $"Sprint {data.SprintReport.SprintName} chưa có issue để xuất.";
            worksheet.ColumnsUsed().AdjustToContents();
            return;
        }

        for (var index = 0; index < data.SprintReport.Rows.Count; index++)
        {
            var rowNumber = index + 2;
            var row = data.SprintReport.Rows[index];
            worksheet.Cell(rowNumber, 1).Value = row.IssueKey;
            worksheet.Cell(rowNumber, 2).Value = row.Title;
            worksheet.Cell(rowNumber, 3).Value = row.IssueType;
            worksheet.Cell(rowNumber, 4).Value = row.Priority;
            worksheet.Cell(rowNumber, 5).Value = row.Status;
            worksheet.Cell(rowNumber, 6).Value = row.Assignee;
            worksheet.Cell(rowNumber, 7).Value = row.StoryPoints;
            if (row.CreatedDate.HasValue)
            {
                worksheet.Cell(rowNumber, 8).Value = row.CreatedDate.Value.ToDateTime(TimeOnly.MinValue);
            }

            if (row.CompletedDate.HasValue)
            {
                worksheet.Cell(rowNumber, 9).Value = row.CompletedDate.Value.ToDateTime(TimeOnly.MinValue);
            }
        }

        ApplyDataSheetStyling(worksheet, 1, data.SprintReport.Rows.Count + 1, SprintReportHeaders.Length);
        worksheet.Range(2, 7, data.SprintReport.Rows.Count + 1, 7).Style.NumberFormat.Format = IntegerFormat;
        worksheet.Range(2, 8, data.SprintReport.Rows.Count + 1, 9).Style.NumberFormat.Format = DateFormat;
    }

    private void BuildVelocitySheet(XLWorkbook workbook, ExcelWorkbookData data)
    {
        var worksheet = workbook.Worksheets.Add(VelocitySheetName);
        BuildDataSheetHeader(worksheet, VelocityHeaders);

        if (data.VelocityRows.Count == 0)
        {
            worksheet.Cell(3, 1).Value = NoVelocityDataText;
            worksheet.ColumnsUsed().AdjustToContents();
            return;
        }

        for (var index = 0; index < data.VelocityRows.Count; index++)
        {
            var rowNumber = index + 2;
            var row = data.VelocityRows[index];
            worksheet.Cell(rowNumber, 1).Value = row.SprintName;
            if (row.StartDate.HasValue)
            {
                worksheet.Cell(rowNumber, 2).Value = row.StartDate.Value.ToDateTime(TimeOnly.MinValue);
            }

            if (row.EndDate.HasValue)
            {
                worksheet.Cell(rowNumber, 3).Value = row.EndDate.Value.ToDateTime(TimeOnly.MinValue);
            }

            worksheet.Cell(rowNumber, 4).Value = row.CommittedStoryPoints;
            worksheet.Cell(rowNumber, 5).Value = row.CompletedStoryPoints;
            worksheet.Cell(rowNumber, 6).Value = row.CompletionRatio;
        }

        ApplyDataSheetStyling(worksheet, 1, data.VelocityRows.Count + 1, VelocityHeaders.Length);
        worksheet.Range(2, 2, data.VelocityRows.Count + 1, 3).Style.NumberFormat.Format = DateFormat;
        worksheet.Range(2, 4, data.VelocityRows.Count + 1, 5).Style.NumberFormat.Format = IntegerFormat;
        worksheet.Range(2, 6, data.VelocityRows.Count + 1, 6).Style.NumberFormat.Format = PercentageFormat;
    }

    private void BuildIssuesSheet(XLWorkbook workbook, ExcelWorkbookData data)
    {
        var worksheet = workbook.Worksheets.Add(AllIssuesSheetName);
        BuildDataSheetHeader(worksheet, AllIssueHeaders);

        if (data.AllIssues.Count == 0)
        {
            worksheet.Cell(3, 1).Value = "Dự án chưa có issue để xuất.";
            worksheet.ColumnsUsed().AdjustToContents();
            return;
        }

        for (var index = 0; index < data.AllIssues.Count; index++)
        {
            var rowNumber = index + 2;
            var row = data.AllIssues[index];
            worksheet.Cell(rowNumber, 1).Value = row.IssueKey;
            worksheet.Cell(rowNumber, 2).Value = row.Title;
            worksheet.Cell(rowNumber, 3).Value = row.IssueType;
            worksheet.Cell(rowNumber, 4).Value = row.Priority;
            worksheet.Cell(rowNumber, 5).Value = row.Status;
            worksheet.Cell(rowNumber, 6).Value = row.Assignee;
            worksheet.Cell(rowNumber, 7).Value = row.SprintName;
            worksheet.Cell(rowNumber, 8).Value = row.StoryPoints;
            worksheet.Cell(rowNumber, 9).Value = row.CreatedDate.ToDateTime(TimeOnly.MinValue);
            worksheet.Cell(rowNumber, 10).Value = row.UpdatedDate.ToDateTime(TimeOnly.MinValue);
        }

        ApplyDataSheetStyling(worksheet, 1, data.AllIssues.Count + 1, AllIssueHeaders.Length);
        worksheet.Range(2, 8, data.AllIssues.Count + 1, 8).Style.NumberFormat.Format = IntegerFormat;
        worksheet.Range(2, 9, data.AllIssues.Count + 1, 10).Style.NumberFormat.Format = DateFormat;
    }

    private static void BuildDataSheetHeader(IXLWorksheet worksheet, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            worksheet.Cell(1, index + 1).Value = headers[index];
        }

        var headerRange = worksheet.Range(1, 1, 1, headers.Count);
        ApplyHeaderStyle(headerRange);
        worksheet.SheetView.FreezeRows(1);
    }

    private static void ApplyHeaderStyle(IXLRange range)
    {
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(HeaderFillHex);
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 11;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void ApplyDataSheetStyling(IXLWorksheet worksheet, int headerRow, int lastRow, int lastColumn)
    {
        ApplyHeaderStyle(worksheet.Range(headerRow, 1, headerRow, lastColumn));
        ApplyZebraStriping(worksheet, headerRow + 1, lastRow, lastColumn);
        worksheet.ColumnsUsed().AdjustToContents();
    }

    private static void ApplySummaryStriping(IXLWorksheet worksheet, int startRow, int endRow)
    {
        for (var row = startRow; row <= endRow; row++)
        {
            worksheet.Range(row, 1, row, 2).Style.Fill.BackgroundColor = row % 2 == 0
                ? XLColor.FromHtml(ZebraFillHex)
                : XLColor.White;
        }
    }

    private static void SetCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.Clear(XLClearOptions.Contents);
                break;
            case DateTime dateTime:
                cell.Value = dateTime;
                break;
            case DateOnly dateOnly:
                cell.Value = dateOnly.ToDateTime(TimeOnly.MinValue);
                break;
            case int integer:
                cell.Value = integer;
                break;
            case double floatingPoint:
                cell.Value = floatingPoint;
                break;
            case decimal decimalValue:
                cell.Value = decimalValue;
                break;
            case bool booleanValue:
                cell.Value = booleanValue;
                break;
            case string text:
                cell.Value = text;
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }

    private static void ApplyZebraStriping(IXLWorksheet worksheet, int firstDataRow, int lastDataRow, int lastColumn)
    {
        for (var row = firstDataRow; row <= lastDataRow; row++)
        {
            worksheet.Range(row, 1, row, lastColumn).Style.Fill.BackgroundColor = row % 2 == 0
                ? XLColor.White
                : XLColor.FromHtml(ZebraFillHex);
        }
    }

    private static Sprint? ResolveTargetSprint(
        IReadOnlyList<Sprint> sprints,
        Sprint? activeSprint,
        int? preferredSprintId)
    {
        if (preferredSprintId.HasValue)
        {
            var preferredSprint = sprints.FirstOrDefault(sprint => sprint.Id == preferredSprintId.Value);
            if (preferredSprint is not null)
            {
                return preferredSprint;
            }
        }

        return activeSprint;
    }

    private static string FormatActiveSprint(Sprint? sprint)
    {
        if (sprint is null)
        {
            return NoActiveSprintText;
        }

        var parts = new List<string> { sprint.Name, FormatSprintState(sprint.State) };
        if (sprint.StartDate.HasValue || sprint.EndDate.HasValue)
        {
            var start = sprint.StartDate?.ToString(DateFormat) ?? "--";
            var end = sprint.EndDate?.ToString(DateFormat) ?? "--";
            parts.Add($"{start} - {end}");
        }

        return string.Join(" | ", parts);
    }

    private static string FormatBoardType(BoardType boardType) => boardType switch
    {
        BoardType.Kanban => "Kanban",
        _ => "Scrum"
    };

    private static string FormatSprintState(SprintState sprintState) => sprintState switch
    {
        SprintState.Active => "Đang chạy",
        SprintState.Closed => "Đã đóng",
        _ => "Đã lên kế hoạch"
    };

    private static string FormatIssueType(IssueType issueType) => issueType switch
    {
        IssueType.Bug => "Bug",
        IssueType.Story => "Story",
        IssueType.Epic => "Epic",
        IssueType.Subtask => "Sub-task",
        _ => "Task"
    };

    private static string FormatPriority(IssuePriority priority) => priority switch
    {
        IssuePriority.Highest => "Cao nhất",
        IssuePriority.High => "Cao",
        IssuePriority.Low => "Thấp",
        IssuePriority.Lowest => "Thấp nhất",
        _ => "Trung bình"
    };

    private static string FormatAssignees(Issue issue)
    {
        if (issue.Assignees.Count == 0)
        {
            return UnassignedText;
        }

        return string.Join(
            ", ",
            issue.Assignees
                .Select(assignee => assignee.User.DisplayName)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
    }

    private static DateOnly? ResolveCompletionDate(
        Issue issue,
        IReadOnlyList<ActivityLogEntity> statusActivities,
        DateOnly? cutoffDate)
    {
        var completedTransition = statusActivities
            .Where(activity => activity.ActionType == ActivityActionType.StatusChanged)
            .OrderBy(activity => activity.OccurredAtUtc)
            .LastOrDefault(activity =>
            {
                var occurredOn = ConvertUtcToLocalDate(activity.OccurredAtUtc);
                return (!cutoffDate.HasValue || occurredOn <= cutoffDate.Value)
                    && ResolveStatusCategories(activity).NewCategory == StatusCategory.Done;
            });

        if (completedTransition is not null)
        {
            return ConvertUtcToLocalDate(completedTransition.OccurredAtUtc);
        }

        if (issue.WorkflowStatus.Category != StatusCategory.Done)
        {
            return null;
        }

        var updatedDate = ConvertUtcToLocalDate(issue.UpdatedAtUtc);
        return cutoffDate.HasValue && updatedDate > cutoffDate.Value
            ? null
            : updatedDate;
    }

    private static (StatusCategory OldCategory, StatusCategory NewCategory) ResolveStatusCategories(ActivityLogEntity activity)
    {
        var metadata = TryParseTransitionMetadata(activity.MetadataJson);
        if (metadata is not null)
        {
            return (metadata.OldCategory, metadata.NewCategory);
        }

        return (
            ResolveStatusCategoryFromName(activity.OldValue),
            ResolveStatusCategoryFromName(activity.NewValue));
    }

    private static TransitionMetadata? TryParseTransitionMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<TransitionMetadata>(metadataJson);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static StatusCategory ResolveStatusCategoryFromName(string? statusName)
    {
        if (string.IsNullOrWhiteSpace(statusName))
        {
            return StatusCategory.ToDo;
        }

        if (string.Equals(statusName, "Done", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCategory.Done;
        }

        if (string.Equals(statusName, "In Progress", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCategory.InProgress;
        }

        return StatusCategory.ToDo;
    }

    private static DateOnly ConvertUtcToLocalDate(DateTime value)
    {
        var utcValue = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        var localValue = TimeZoneInfo.ConvertTimeFromUtc(utcValue, TimeZoneInfo.Local);
        return DateOnly.FromDateTime(localValue);
    }

    private static DateOnly GetTodayLocalDate() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Local));

    private static DateTime GetSprintSortDate(Sprint sprint)
    {
        if (sprint.ClosedAtUtc.HasValue)
        {
            return sprint.ClosedAtUtc.Value;
        }

        if (sprint.EndDate.HasValue)
        {
            return sprint.EndDate.Value.ToDateTime(TimeOnly.MinValue);
        }

        if (sprint.StartDate.HasValue)
        {
            return sprint.StartDate.Value.ToDateTime(TimeOnly.MinValue);
        }

        return DateTime.MinValue;
    }

    private static void EnsureTargetDirectoryExists(string destinationPath)
    {
        var directoryPath = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    private static WorkbookContext CreateWorkbookContext()
    {
        var fallbackFontPath = ResolveFallbackFontPath();
        if (string.IsNullOrWhiteSpace(fallbackFontPath))
        {
            return new WorkbookContext(new XLWorkbook(), fallbackFontStream: null);
        }

        var fallbackFontStream = File.OpenRead(fallbackFontPath);
        var loadOptions = new LoadOptions
        {
            GraphicEngine = DefaultGraphicEngine.CreateOnlyWithFonts(fallbackFontStream)
        };

        return new WorkbookContext(new XLWorkbook(loadOptions), fallbackFontStream);
    }

    private static string? ResolveFallbackFontPath()
    {
        var fontDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (string.IsNullOrWhiteSpace(fontDirectory))
        {
            return null;
        }

        var candidateFonts = new[]
        {
            "arial.ttf",
            "segoeui.ttf",
            "tahoma.ttf"
        };

        foreach (var fontFileName in candidateFonts)
        {
            var fullPath = Path.Combine(fontDirectory, fontFileName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static void ValidateRequest(ExcelReportExportRequest request)
    {
        if (request.ProjectId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.ProjectId), "ProjectId phải lớn hơn 0.");
        }

        if (string.IsNullOrWhiteSpace(request.DestinationPath))
        {
            throw new ArgumentException("DestinationPath là bắt buộc.", nameof(request.DestinationPath));
        }
    }

    private async Task EnsurePermissionAsync(int projectId, Permission permission, CancellationToken cancellationToken)
    {
        var userId = _currentUserContext.RequireUserId();
        if (!await _permissionService.HasPermissionAsync(userId, projectId, permission, cancellationToken))
        {
            throw new UnauthorizedAccessException("Current user does not have permission to export this report.");
        }
    }

    private sealed record SummarySheetData(
        string ProjectName,
        string ProjectKey,
        string BoardType,
        string ActiveSprint,
        int TotalIssues,
        int DoneIssues,
        double CompletionRatio,
        int MemberCount,
        DateOnly ExportedOn);

    private sealed record SprintReportSheetData(
        string? SprintName,
        IReadOnlyList<SprintReportSheetRow> Rows);

    private sealed record SprintReportSheetRow(
        string IssueKey,
        string Title,
        string IssueType,
        string Priority,
        string Status,
        string Assignee,
        int StoryPoints,
        DateOnly? CreatedDate,
        DateOnly? CompletedDate);

    private sealed record VelocitySheetRow(
        string SprintName,
        DateOnly? StartDate,
        DateOnly? EndDate,
        int CommittedStoryPoints,
        int CompletedStoryPoints,
        double CompletionRatio);

    private sealed record AllIssueSheetRow(
        string IssueKey,
        string Title,
        string IssueType,
        string Priority,
        string Status,
        string Assignee,
        string SprintName,
        int StoryPoints,
        DateOnly CreatedDate,
        DateOnly UpdatedDate);

    private sealed record ExcelWorkbookData(
        SummarySheetData Summary,
        SprintReportSheetData SprintReport,
        IReadOnlyList<VelocitySheetRow> VelocityRows,
        IReadOnlyList<AllIssueSheetRow> AllIssues);

    private sealed record TransitionMetadata(
        int OldStatusId,
        string OldStatusName,
        StatusCategory OldCategory,
        int NewStatusId,
        string NewStatusName,
        StatusCategory NewCategory);

    private sealed class WorkbookContext : IDisposable
    {
        private readonly Stream? _fallbackFontStream;

        public WorkbookContext(XLWorkbook workbook, Stream? fallbackFontStream)
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
