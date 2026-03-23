using JiraClone.Application.Jql;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public sealed class JqlEditorControl : UserControl
{
    private readonly RichTextBox _editor = new()
    {
        BorderStyle = BorderStyle.None,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 11.5f, FontStyle.Regular),
        AcceptsTab = true,
        Multiline = true,
        WordWrap = true,
        ScrollBars = RichTextBoxScrollBars.Vertical,
        BackColor = JiraTheme.BgSurface,
        ForeColor = JiraTheme.TextPrimary
    };
    private readonly ListBox _suggestions = new()
    {
        Visible = false,
        IntegralHeight = false,
        Height = 164,
        Width = 300,
        BorderStyle = BorderStyle.FixedSingle,
        Font = JiraTheme.FontSmall,
        BackColor = JiraTheme.BgSurface,
        ForeColor = JiraTheme.TextPrimary
    };
    private readonly JqlLexer _lexer = new();
    private bool _highlighting;

    public JqlEditorControl()
    {
        BackColor = JiraTheme.BgSurface;
        BorderStyle = BorderStyle.FixedSingle;
        Padding = new Padding(10, 8, 10, 8);
        Height = 104;

        Controls.Add(_editor);
        Controls.Add(_suggestions);

        _editor.TextChanged += (_, _) =>
        {
            HighlightSyntax();
            QueryChanged?.Invoke(this, EventArgs.Empty);
            UpdateSuggestions();
        };
        _editor.SelectionChanged += (_, _) => UpdateSuggestions();
        _editor.KeyDown += HandleEditorKeyDown;
        _editor.Resize += (_, _) => PositionSuggestions();
        _suggestions.DoubleClick += (_, _) => AcceptSuggestion();
        _suggestions.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
            {
                e.SuppressKeyPress = true;
                AcceptSuggestion();
            }
        };
    }

    public event EventHandler? QueryChanged;

    public Func<string, int, IReadOnlyList<string>>? SuggestionProvider { get; set; }

    public string QueryText
    {
        get => _editor.Text;
        set
        {
            if (string.Equals(_editor.Text, value, StringComparison.Ordinal))
            {
                return;
            }

            _editor.Text = value ?? string.Empty;
            _editor.SelectionStart = _editor.TextLength;
        }
    }

    public RichTextBox Editor => _editor;

    public void FocusEditor() => _editor.Focus();

    private void HandleEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (_suggestions.Visible)
        {
            if (e.KeyCode == Keys.Down)
            {
                e.SuppressKeyPress = true;
                var next = Math.Min(_suggestions.Items.Count - 1, Math.Max(0, _suggestions.SelectedIndex + 1));
                _suggestions.SelectedIndex = next;
                return;
            }

            if (e.KeyCode == Keys.Up)
            {
                e.SuppressKeyPress = true;
                var next = Math.Max(0, _suggestions.SelectedIndex - 1);
                _suggestions.SelectedIndex = next;
                return;
            }

            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
            {
                e.SuppressKeyPress = true;
                AcceptSuggestion();
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                _suggestions.Visible = false;
                return;
            }
        }
    }

    private void HighlightSyntax()
    {
        if (_highlighting)
        {
            return;
        }

        _highlighting = true;
        try
        {
            var selectionStart = _editor.SelectionStart;
            var selectionLength = _editor.SelectionLength;
            _editor.SuspendLayout();
            _editor.SelectAll();
            _editor.SelectionColor = JiraTheme.TextPrimary;

            IReadOnlyList<JqlToken> tokens;
            try
            {
                tokens = _lexer.Tokenize(_editor.Text);
            }
            catch
            {
                _editor.Select(selectionStart, selectionLength);
                return;
            }

            for (var index = 0; index < tokens.Count; index++)
            {
                var token = tokens[index];
                if (token.Kind == JqlTokenKind.EndOfInput || token.Length == 0)
                {
                    continue;
                }

                _editor.Select(token.Position, token.Length);
                _editor.SelectionColor = GetTokenColor(tokens, index);
            }

            _editor.Select(selectionStart, selectionLength);
            _editor.SelectionColor = JiraTheme.TextPrimary;
        }
        finally
        {
            _editor.ResumeLayout();
            _highlighting = false;
        }
    }

    private Color GetTokenColor(IReadOnlyList<JqlToken> tokens, int index)
    {
        var token = tokens[index];
        return token.Kind switch
        {
            JqlTokenKind.And or JqlTokenKind.Or or JqlTokenKind.In or JqlTokenKind.Not or JqlTokenKind.Order or JqlTokenKind.By or JqlTokenKind.Asc or JqlTokenKind.Desc => JiraTheme.Warning,
            JqlTokenKind.Equals or JqlTokenKind.NotEquals or JqlTokenKind.GreaterThan or JqlTokenKind.GreaterThanOrEqual or JqlTokenKind.LessThan or JqlTokenKind.LessThanOrEqual => JiraTheme.Warning,
            JqlTokenKind.String or JqlTokenKind.Number or JqlTokenKind.RelativeDate => JiraTheme.Success,
            JqlTokenKind.Identifier when index + 1 < tokens.Count && (tokens[index + 1].Kind is JqlTokenKind.Equals or JqlTokenKind.NotEquals or JqlTokenKind.GreaterThan or JqlTokenKind.GreaterThanOrEqual or JqlTokenKind.LessThan or JqlTokenKind.LessThanOrEqual or JqlTokenKind.In or JqlTokenKind.Not or JqlTokenKind.OpenParen) => JiraTheme.PrimaryActive,
            JqlTokenKind.Identifier => JiraTheme.Success,
            _ => JiraTheme.TextPrimary
        };
    }

    private void UpdateSuggestions()
    {
        var provider = SuggestionProvider;
        if (provider is null)
        {
            _suggestions.Visible = false;
            return;
        }

        var suggestions = provider(_editor.Text, _editor.SelectionStart)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        if (suggestions.Count == 0)
        {
            _suggestions.Visible = false;
            return;
        }

        _suggestions.BeginUpdate();
        _suggestions.Items.Clear();
        foreach (var suggestion in suggestions)
        {
            _suggestions.Items.Add(suggestion);
        }
        _suggestions.SelectedIndex = 0;
        _suggestions.EndUpdate();
        PositionSuggestions();
        _suggestions.Visible = true;
        _suggestions.BringToFront();
    }

    private void PositionSuggestions()
    {
        _suggestions.Left = Padding.Left;
        _suggestions.Top = Math.Max(Padding.Top + 32, Height - _suggestions.Height - Padding.Bottom + 8);
        _suggestions.Width = Math.Min(380, Math.Max(260, Width - Padding.Horizontal - 20));
    }

    private void AcceptSuggestion()
    {
        if (!_suggestions.Visible || _suggestions.SelectedItem is not string suggestion)
        {
            return;
        }

        var (start, length) = GetReplacementRange();
        _editor.Select(start, length);
        _editor.SelectedText = suggestion;
        _editor.SelectionStart = start + suggestion.Length;
        _editor.SelectionLength = 0;
        _suggestions.Visible = false;
        _editor.Focus();
    }

    private (int Start, int Length) GetReplacementRange()
    {
        var caret = _editor.SelectionStart;
        var text = _editor.Text;
        var start = caret;
        while (start > 0 && IsSuggestionChar(text[start - 1]))
        {
            start--;
        }

        var end = caret;
        while (end < text.Length && IsSuggestionChar(text[end]))
        {
            end++;
        }

        return (start, end - start);
    }

    private static bool IsSuggestionChar(char value) => char.IsLetterOrDigit(value) || value is '_' or '.' or '-';
}

