namespace JiraClone.Application.Reports;

public interface IExcelExportService
{
    Task ExportProjectReportAsync(ExcelReportExportRequest request, CancellationToken cancellationToken = default);
}
