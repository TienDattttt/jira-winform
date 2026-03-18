using System.Globalization;

namespace JiraClone.Application.Jql;

public sealed class JqlParser
{
    private readonly JqlLexer _lexer = new();
    private IReadOnlyList<JqlToken> _tokens = Array.Empty<JqlToken>();
    private int _index;

    public JqlQuery Parse(string? input)
    {
        _tokens = _lexer.Tokenize(input);
        _index = 0;

        JqlExpression? filter = null;
        if (!Match(JqlTokenKind.EndOfInput) && !IsOrderBy())
        {
            filter = ParseOrExpression();
        }

        var sorts = ParseOrderBy();
        Expect(JqlTokenKind.EndOfInput, "Unexpected trailing tokens.");
        return new JqlQuery(filter, sorts);
    }

    private JqlExpression ParseOrExpression()
    {
        var expression = ParseAndExpression();
        while (Match(JqlTokenKind.Or))
        {
            var right = ParseAndExpression();
            expression = new JqlBinaryExpression(expression, JqlLogicalOperator.Or, right);
        }

        return expression;
    }

    private JqlExpression ParseAndExpression()
    {
        var expression = ParsePrimaryExpression();
        while (Match(JqlTokenKind.And))
        {
            var right = ParsePrimaryExpression();
            expression = new JqlBinaryExpression(expression, JqlLogicalOperator.And, right);
        }

        return expression;
    }

    private JqlExpression ParsePrimaryExpression()
    {
        if (Match(JqlTokenKind.OpenParen))
        {
            var inner = ParseOrExpression();
            Expect(JqlTokenKind.CloseParen, "Expected ')' to close grouped expression.");
            return inner;
        }

        return ParseCondition();
    }

    private JqlExpression ParseCondition()
    {
        var field = Expect(JqlTokenKind.Identifier, "Expected a field name.");
        var op = ParseOperator();

        if (op is JqlComparisonOperator.In or JqlComparisonOperator.NotIn)
        {
            Expect(JqlTokenKind.OpenParen, "Expected '(' after IN operator.");
            var values = new List<JqlValue> { ParseValue() };
            while (Match(JqlTokenKind.Comma))
            {
                values.Add(ParseValue());
            }

            Expect(JqlTokenKind.CloseParen, "Expected ')' after IN list.");
            return new JqlCondition(field.Text, op, values);
        }

        return new JqlCondition(field.Text, op, [ParseValue()]);
    }

    private JqlComparisonOperator ParseOperator()
    {
        if (Match(JqlTokenKind.Equals)) return JqlComparisonOperator.Equals;
        if (Match(JqlTokenKind.NotEquals)) return JqlComparisonOperator.NotEquals;
        if (Match(JqlTokenKind.GreaterThan)) return JqlComparisonOperator.GreaterThan;
        if (Match(JqlTokenKind.GreaterThanOrEqual)) return JqlComparisonOperator.GreaterThanOrEqual;
        if (Match(JqlTokenKind.LessThan)) return JqlComparisonOperator.LessThan;
        if (Match(JqlTokenKind.LessThanOrEqual)) return JqlComparisonOperator.LessThanOrEqual;
        if (Match(JqlTokenKind.In)) return JqlComparisonOperator.In;
        if (Match(JqlTokenKind.Not))
        {
            Expect(JqlTokenKind.In, "Expected IN after NOT.");
            return JqlComparisonOperator.NotIn;
        }

        throw Error(Current, "Expected an operator such as =, IN, >=, <=, >, or <.");
    }

    private JqlValue ParseValue()
    {
        if (Match(JqlTokenKind.String, out var stringToken))
        {
            return new JqlStringValue(stringToken.Text);
        }

        if (Match(JqlTokenKind.Number, out var numberToken))
        {
            if (!decimal.TryParse(numberToken.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            {
                throw Error(numberToken, "Invalid number literal.");
            }

            return new JqlNumberValue(number);
        }

        if (Match(JqlTokenKind.RelativeDate, out var relativeToken))
        {
            var unit = char.ToLowerInvariant(relativeToken.Text[^1]);
            if (!int.TryParse(relativeToken.Text[1..^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
            {
                throw Error(relativeToken, "Invalid relative date literal.");
            }

            return new JqlRelativeDateValue(-amount, unit);
        }

        if (Match(JqlTokenKind.Identifier, out var identifierToken))
        {
            if (Match(JqlTokenKind.OpenParen))
            {
                Expect(JqlTokenKind.CloseParen, "Expected ')' to close function call.");
                return new JqlFunctionValue(identifierToken.Text);
            }

            return new JqlStringValue(identifierToken.Text);
        }

        throw Error(Current, "Expected a value.");
    }

    private IReadOnlyList<JqlSortClause> ParseOrderBy()
    {
        var sorts = new List<JqlSortClause>();
        if (!Match(JqlTokenKind.Order))
        {
            return sorts;
        }

        Expect(JqlTokenKind.By, "Expected BY after ORDER.");
        do
        {
            var field = Expect(JqlTokenKind.Identifier, "Expected a field name after ORDER BY.");
            var descending = Match(JqlTokenKind.Desc);
            if (!descending)
            {
                Match(JqlTokenKind.Asc);
            }

            sorts.Add(new JqlSortClause(field.Text, descending));
        }
        while (Match(JqlTokenKind.Comma));

        return sorts;
    }

    private bool IsOrderBy() => Current.Kind == JqlTokenKind.Order && Peek(1).Kind == JqlTokenKind.By;

    private JqlToken Current => Peek(0);

    private JqlToken Peek(int offset)
    {
        var target = _index + offset;
        return target >= 0 && target < _tokens.Count ? _tokens[target] : _tokens[^1];
    }

    private bool Match(JqlTokenKind kind)
    {
        if (Current.Kind != kind)
        {
            return false;
        }

        _index++;
        return true;
    }

    private bool Match(JqlTokenKind kind, out JqlToken token)
    {
        token = Current;
        if (token.Kind != kind)
        {
            return false;
        }

        _index++;
        return true;
    }

    private JqlToken Expect(JqlTokenKind kind, string message)
    {
        if (Current.Kind == kind)
        {
            var token = Current;
            _index++;
            return token;
        }

        throw Error(Current, message);
    }

    private static JqlParseException Error(JqlToken token, string message) => new(message, token.Position, Math.Max(token.Length, 1));
}
