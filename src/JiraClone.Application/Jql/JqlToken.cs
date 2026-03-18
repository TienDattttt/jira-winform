namespace JiraClone.Application.Jql;

public enum JqlTokenKind
{
    Identifier,
    String,
    Number,
    RelativeDate,
    OpenParen,
    CloseParen,
    Comma,
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    And,
    Or,
    In,
    Not,
    Order,
    By,
    Asc,
    Desc,
    EndOfInput
}

public sealed record JqlToken(JqlTokenKind Kind, string Text, int Position, int Length);
