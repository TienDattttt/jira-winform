using System.Reflection;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public class MarkdownEditorControl : UserControl
{
    private const string EditorResourceName = "JiraClone.WinForms.Assets.MarkdownEditorHost.html";
    private readonly WebBrowser _browser = new()
    {
        Dock = DockStyle.Fill,
        AllowWebBrowserDrop = false,
        IsWebBrowserContextMenuEnabled = false,
        ScriptErrorsSuppressed = true,
        WebBrowserShortcutsEnabled = true
    };

    private bool _isReady;
    private string _pendingContent = string.Empty;
    private bool _pendingReadOnly;

    public MarkdownEditorControl()
    {
        DoubleBuffered = true;
        BackColor = JiraTheme.BgSurface;
        Controls.Add(_browser);

        _browser.DocumentCompleted += Browser_DocumentCompleted;
        _browser.Leave += (_, _) => EditorLeave?.Invoke(this, EventArgs.Empty);
        _browser.DocumentText = LoadHostDocument();
    }

    public event EventHandler? EditorLeave;

    public void SetContent(string? markdown)
    {
        _pendingContent = markdown ?? string.Empty;
        if (_isReady)
        {
            InvokeScript("setValue", _pendingContent);
        }
    }

    public string GetContent()
    {
        if (!_isReady)
        {
            return _pendingContent;
        }

        var value = InvokeScript("getValue")?.ToString() ?? string.Empty;
        _pendingContent = value;
        return value;
    }

    public void SetReadOnly(bool isReadOnly)
    {
        _pendingReadOnly = isReadOnly;
        if (_isReady)
        {
            InvokeScript("setReadOnly", isReadOnly);
        }
    }

    public void FocusEditor()
    {
        _browser.Focus();
        if (_isReady)
        {
            InvokeScript("focusEditor");
        }
    }

    private void Browser_DocumentCompleted(object? sender, WebBrowserDocumentCompletedEventArgs e)
    {
        if (_isReady)
        {
            return;
        }

        _isReady = true;
        InvokeScript("setValue", _pendingContent);
        InvokeScript("setReadOnly", _pendingReadOnly);
    }

    private object? InvokeScript(string scriptName, params object[] args)
    {
        try
        {
            return _browser.Document?.InvokeScript(scriptName, args);
        }
        catch
        {
            return null;
        }
    }

    private static string LoadHostDocument()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EditorResourceName);
        if (stream is null)
        {
            return "<html><body><textarea id='markdown-input' style='width:100%;height:100%;font-family:Segoe UI;'></textarea><script>function setValue(value){document.getElementById('markdown-input').value=value||'';}function getValue(){return document.getElementById('markdown-input').value;}function setReadOnly(value){document.getElementById('markdown-input').readOnly=!!value;}</script></body></html>";
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

