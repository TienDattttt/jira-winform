namespace JiraClone.Application.Reports;

public sealed record ExcelReportExportRequest(
    int ProjectId,
    string DestinationPath,
    int? PreferredSprintId = null);
