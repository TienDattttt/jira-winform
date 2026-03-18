using System.Globalization;

namespace JiraClone.Application.Jql;

public sealed class JqlLexer
{
    public IReadOnlyList<JqlToken> Tokenize(string? input)
    {
        var source = input ?? string.Empty;
        var tokens = new List<JqlToken>();
        var index = 0;

        while (index < source.Length)
        {
            var current = source[index];
            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (current == '(')
            {
                tokens.Add(new JqlToken(JqlTokenKind.OpenParen, "(", index, 1));
                index++;
                continue;
            }

            if (current == ')')
            {
                tokens.Add(new JqlToken(JqlTokenKind.CloseParen, ")", index, 1));
                index++;
                continue;
            }

            if (current == ',')
            {
                tokens.Add(new JqlToken(JqlTokenKind.Comma, ",", index, 1));
                index++;
                continue;
            }

            if (current == '!' && Peek(source, index + 1) == '=')
            {
                tokens.Add(new JqlToken(JqlTokenKind.NotEquals, "!=", index, 2));
                index += 2;
                continue;
            }

            if (current == '>' && Peek(source, index + 1) == '=')
            {
                tokens.Add(new JqlToken(JqlTokenKind.GreaterThanOrEqual, ">=", index, 2));
                index += 2;
                continue;
            }

            if (current == '<' && Peek(source, index + 1) == '=')
            {
                tokens.Add(new JqlToken(JqlTokenKind.LessThanOrEqual, "<=", index, 2));
                index += 2;
                continue;
            }

            if (current == '=')
            {
                tokens.Add(new JqlToken(JqlTokenKind.Equals, "=", index, 1));
                index++;
                continue;
            }

            if (current == '>')
            {
                tokens.Add(new JqlToken(JqlTokenKind.GreaterThan, ">", index, 1));
                index++;
                continue;
            }

            if (current == '<')
            {
                tokens.Add(new JqlToken(JqlTokenKind.LessThan, "<", index, 1));
                index++;
                continue;
            }

            if (current == '"' || current == '\'')
            {
                var quote = current;
                var start = index++;
                var buffer = new List<char>();
                while (index < source.Length && source[index] != quote)
                {
                    if (source[index] == '\\' && index + 1 < source.Length)
                    {
                        index++;
                    }

                    buffer.Add(source[index]);
                    index++;
                }

                if (index >= source.Length)
                {
                    throw new JqlParseException("Unterminated string literal.", start, source.Length - start);
                }

                index++;
                tokens.Add(new JqlToken(JqlTokenKind.String, new string(buffer.ToArray()), start, index - start));
                continue;
            }

            if (current == '-' && index + 2 < source.Length && char.IsDigit(source[index + 1]))
            {
                var start = index;
                index++;
                while (index < source.Length && char.IsDigit(source[index]))
                {
                    index++;
                }

                if (index < source.Length && char.IsLetter(source[index]))
                {
                    index++;
                    tokens.Add(new JqlToken(JqlTokenKind.RelativeDate, source[start..index], start, index - start));
                    continue;
                }

                throw new JqlParseException("Invalid relative date literal.", start, index - start);
            }

            if (char.IsDigit(current))
            {
                var start = index;
                index++;
                while (index < source.Length && (char.IsDigit(source[index]) || source[index] == '.'))
                {
                    index++;
                }

                tokens.Add(new JqlToken(JqlTokenKind.Number, source[start..index], start, index - start));
                continue;
            }

            if (IsIdentifierStart(current))
            {
                var start = index;
                index++;
                while (index < source.Length && IsIdentifierPart(source[index]))
                {
                    index++;
                }

                var text = source[start..index];
                tokens.Add(new JqlToken(GetKeywordKind(text), text, start, index - start));
                continue;
            }

            throw new JqlParseException($"Unexpected character '{current}'.", index);
        }

        tokens.Add(new JqlToken(JqlTokenKind.EndOfInput, string.Empty, source.Length, 0));
        return tokens;
    }

    private static bool IsIdentifierStart(char value) => char.IsLetter(value) || value == '_';

    private static bool IsIdentifierPart(char value) => char.IsLetterOrDigit(value) || value is '_' or '-' or '.';

    private static char Peek(string input, int index) => index >= 0 && index < input.Length ? input[index] : '\0';

    private static JqlTokenKind GetKeywordKind(string text)
    {
        return text.ToUpperInvariant() switch
        {
            "AND" => JqlTokenKind.And,
            "OR" => JqlTokenKind.Or,
            "IN" => JqlTokenKind.In,
            "NOT" => JqlTokenKind.Not,
            "ORDER" => JqlTokenKind.Order,
            "BY" => JqlTokenKind.By,
            "ASC" => JqlTokenKind.Asc,
            "DESC" => JqlTokenKind.Desc,
            _ => JqlTokenKind.Identifier
        };
    }
}
