using System.Diagnostics;
using System.Net;
using JiraClone.Application.Common;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public class MarkdownViewerControl : UserControl
{
    private const string EmptyMessage = "Thêm mô tả...";
    private readonly WebBrowser _browser = new()
    {
        Dock = DockStyle.Fill,
        AllowWebBrowserDrop = false,
        IsWebBrowserContextMenuEnabled = false,
        ScriptErrorsSuppressed = true,
        WebBrowserShortcutsEnabled = false
    };

    public MarkdownViewerControl()
    {
        DoubleBuffered = true;
        BackColor = JiraTheme.BgSurface;
        Controls.Add(_browser);
        _browser.Navigating += HandleNavigating;
        SetContent(null);
    }

    public void SetContent(string? markdown)
    {
        var html = MarkdownHtmlRenderer.Render(markdown);
        _browser.DocumentText = BuildDocument(html);
    }

    private void HandleNavigating(object? sender, WebBrowserNavigatingEventArgs e)
    {
        if (e.Url is null)
        {
            return;
        }

        if (string.Equals(e.Url.Scheme, "about", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        e.Cancel = true;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Url.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private static string BuildDocument(string? html)
    {
        var body = string.IsNullOrWhiteSpace(html)
            ? $"<div class=\"empty\">{WebUtility.HtmlEncode(EmptyMessage)}</div>"
            : html;

        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <meta http-equiv=""X-UA-Compatible"" content=""IE=11"" />
    <style>
        html, body {{
            margin: 0;
            padding: 0;
            background: #ffffff;
            color: #1e1f21;
            font-family: 'Segoe UI', sans-serif;
            font-size: 14px;
            line-height: 1.65;
        }}

        body {{
            padding: 14px 16px;
        }}

        h1, h2, h3, h4, h5, h6 {{
            margin: 0 0 12px;
            color: #1c2b42;
        }}

        p, ul, ol, pre, blockquote {{
            margin: 0 0 12px;
        }}

        ul, ol {{
            padding-left: 24px;
        }}

        a {{
            color: #1868db;
            text-decoration: none;
        }}

        a:hover {{
            text-decoration: underline;
        }}

        pre, code {{
            font-family: Consolas, 'Segoe UI', monospace;
        }}

        code {{
            background: #f0f1f2;
            padding: 2px 5px;
            border-radius: 4px;
        }}

        pre {{
            background: #f8f8f8;
            border: 1px solid #dddee1;
            border-radius: 4px;
            padding: 12px;
            overflow-x: auto;
        }}

        pre code {{
            background: transparent;
            padding: 0;
        }}

        blockquote {{
            padding-left: 12px;
            border-left: 4px solid #4688ec;
            color: #6b6e76;
        }}

        table {{
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 12px;
        }}

        th, td {{
            border: 1px solid #dddee1;
            padding: 8px 10px;
            text-align: left;
        }}

        th {{
            background: #f8f8f8;
        }}

        .empty {{
            border: 1px dashed #dddee1;
            border-radius: 4px;
            padding: 18px;
            color: #6b6e76;
            background: #f8f8f8;
        }}
    </style>
</head>
<body>
{body}
</body>
</html>";
    }
}
