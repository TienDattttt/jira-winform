using Markdig;

namespace JiraClone.Application.Common;

public static class MarkdownHtmlRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static string? Normalize(string? markdown)
    {
        return string.IsNullOrWhiteSpace(markdown)
            ? null
            : markdown.Trim();
    }

    public static string? Render(string? markdown)
    {
        var normalized = Normalize(markdown);
        if (normalized is null)
        {
            return null;
        }

        return Markdown.ToHtml(normalized, Pipeline);
    }
}
