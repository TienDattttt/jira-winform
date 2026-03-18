using JiraClone.Application.Jql;

namespace JiraClone.Tests.Application;

public class JqlParserTests
{
    private readonly JqlParser _parser = new();

    [Fact]
    public void Parse_BuildsConditionTreeAndOrderBy()
    {
        var query = _parser.Parse("project = \"PROJ\" AND status = \"In Progress\" ORDER BY priority DESC");

        var binary = Assert.IsType<JqlBinaryExpression>(query.Filter);
        var left = Assert.IsType<JqlCondition>(binary.Left);
        var right = Assert.IsType<JqlCondition>(binary.Right);

        Assert.Equal(JqlLogicalOperator.And, binary.Operator);
        Assert.Equal("project", left.Field);
        Assert.Equal(JqlComparisonOperator.Equals, left.Operator);
        Assert.Equal("status", right.Field);
        Assert.Single(query.Sorts);
        Assert.Equal("priority", query.Sorts[0].Field);
        Assert.True(query.Sorts[0].Descending);
    }

    [Fact]
    public void Parse_SupportsInListsAndFunctions()
    {
        var query = _parser.Parse("assignee = currentUser() AND priority in (High, Highest)");

        var binary = Assert.IsType<JqlBinaryExpression>(query.Filter);
        var assignee = Assert.IsType<JqlCondition>(binary.Left);
        var priority = Assert.IsType<JqlCondition>(binary.Right);

        var function = Assert.IsType<JqlFunctionValue>(Assert.Single(assignee.Values));
        Assert.Equal("currentUser", function.Name);
        Assert.Equal(JqlComparisonOperator.In, priority.Operator);
        Assert.Collection(priority.Values,
            value => Assert.Equal("High", Assert.IsType<JqlStringValue>(value).Value),
            value => Assert.Equal("Highest", Assert.IsType<JqlStringValue>(value).Value));
    }

    [Fact]
    public void Parse_ReportsPositionForInvalidQuery()
    {
        var exception = Assert.Throws<JqlParseException>(() => _parser.Parse("status ="));

        Assert.True(exception.Position >= 7);
    }
}
