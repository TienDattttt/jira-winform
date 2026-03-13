using JiraClone.Application.Models;

namespace JiraClone.WinForms.ViewModels;

public sealed class BoardViewModel
{
    public string ProjectName { get; init; } = string.Empty;
    public IReadOnlyList<BoardColumnDto> Columns { get; init; } = Array.Empty<BoardColumnDto>();
}
