using System.Text.RegularExpressions;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;

namespace JiraClone.Tests.Domain;

public class IssueTests
{
    [Fact]
    public void Issue_DefaultState_StatusIsBacklog()
    {
        // Arrange
        var issue = new Issue();

        // Act / Assert
        Assert.Equal(IssueStatus.Backlog, issue.Status);
    }

    [Fact]
    public void Issue_DefaultState_PriorityIsMedium()
    {
        // Arrange
        var issue = new Issue();

        // Act / Assert
        Assert.Equal(IssuePriority.Medium, issue.Priority);
    }

    [Fact]
    public void StoryPoints_NegativeValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var issue = new Issue();

        // Act
        Action act = () => issue.StoryPoints = -1;

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void IssueKey_ProjectSequenceFormat_MatchesExpectedPattern()
    {
        // Arrange
        var issue = new Issue { IssueKey = "PROJ-1" };

        // Act
        var matches = Regex.IsMatch(issue.IssueKey, "^[A-Z]+-\\d+$");

        // Assert
        Assert.True(matches);
    }
}
