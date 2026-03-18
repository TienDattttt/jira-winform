namespace JiraClone.Application.Jql;

public sealed record JqlQuery(JqlExpression? Filter, IReadOnlyList<JqlSortClause> Sorts);

public abstract record JqlExpression;

public sealed record JqlBinaryExpression(JqlExpression Left, JqlLogicalOperator Operator, JqlExpression Right) : JqlExpression;

public sealed record JqlCondition(string Field, JqlComparisonOperator Operator, IReadOnlyList<JqlValue> Values) : JqlExpression;

public sealed record JqlSortClause(string Field, bool Descending);

public enum JqlLogicalOperator
{
    And,
    Or
}

public enum JqlComparisonOperator
{
    Equals,
    NotEquals,
    In,
    NotIn,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

public abstract record JqlValue;

public sealed record JqlStringValue(string Value) : JqlValue;

public sealed record JqlNumberValue(decimal Value) : JqlValue;

public sealed record JqlRelativeDateValue(int Amount, char Unit) : JqlValue;

public sealed record JqlFunctionValue(string Name) : JqlValue;
