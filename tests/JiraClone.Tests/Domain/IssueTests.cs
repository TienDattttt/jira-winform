using System.Text.RegularExpressions;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;

namespace JiraClone.Tests.Domain;

public class IssueTests
{
    [Fact]
    public void Issue_DefaultState_BoardPositionStartsAtOne()
    {
        var issue = new Issue();

        Assert.Equal(1m, issue.BoardPosition);
        Assert.Equal(0, issue.WorkflowStatusId);
    }

    [Fact]
    public void Issue_DefaultState_PriorityIsMedium()
    {
        var issue = new Issue();

        Assert.Equal(IssuePriority.Medium, issue.Priority);
    }

    [Fact]
    public void StoryPoints_NegativeValue_ThrowsArgumentOutOfRangeException()
    {
        var issue = new Issue();

        Action act = () => issue.StoryPoints = -1;

        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void IssueKey_ProjectSequenceFormat_MatchesExpectedPattern()
    {
        var issue = new Issue { IssueKey = "PROJ-1" };

        var matches = Regex.IsMatch(issue.IssueKey, "^[A-Z]+-\\d+$");

        Assert.True(matches);
    }
}
