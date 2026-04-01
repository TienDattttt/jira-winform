using JiraClone.Domain.Enums;

namespace JiraClone.WinForms.Helpers;

public static class IssueDisplayText
{
    public static string TranslateStatus(string? status) => status switch
    {
        null => string.Empty,
        "" => string.Empty,
        "Backlog" or "BACKLOG" or "Todo" or "TODO" => "Tồn đọng",
        "Selected" or "SELECTED" => "Đã chọn",
        "In Progress" or "IN PROGRESS" => "Đang làm",
        "Done" or "DONE" => "Hoàn thành",
        _ => status
    };

    public static string TranslateStatus(IssueStatus status) => status switch
    {
        IssueStatus.Backlog => "Tồn đọng",
        IssueStatus.Selected => "Đã chọn",
        IssueStatus.InProgress => "Đang làm",
        IssueStatus.Done => "Hoàn thành",
        _ => status.ToString()
    };

    public static string TranslateType(IssueType type) => type switch
    {
        IssueType.Task => "Công việc",
        IssueType.Bug => "Lỗi",
        IssueType.Story => "Câu chuyện",
        IssueType.Epic => "Epic",
        IssueType.Subtask => "Công việc con",
        _ => type.ToString()
    };

    public static string TranslateType(string? type) => type switch
    {
        null => string.Empty,
        "" => string.Empty,
        "Task" or "TASK" => "Công việc",
        "Bug" or "BUG" => "Lỗi",
        "Story" or "STORY" => "Câu chuyện",
        "Epic" or "EPIC" => "Epic",
        "Subtask" or "SUBTASK" => "Công việc con",
        _ => type
    };
}
