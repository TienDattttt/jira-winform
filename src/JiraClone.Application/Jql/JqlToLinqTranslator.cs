using System.Linq.Expressions;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;

namespace JiraClone.Application.Jql;

public sealed record JqlTranslationContext(int ProjectId, int CurrentUserId, string CurrentUserName, string CurrentUserDisplayName, DateTime NowUtc);

public sealed class JqlToLinqTranslator
{
    public IQueryable<Issue> Apply(IQueryable<Issue> source, JqlQuery query, JqlTranslationContext context)
    {
        var result = source;
        if (query.Filter is not null)
        {
            result = result.Where(BuildPredicate(query.Filter, context));
        }

        if (query.Sorts.Count == 0)
        {
            return result
                .OrderBy(issue => issue.WorkflowStatus.DisplayOrder)
                .ThenBy(issue => issue.BoardPosition)
                .ThenByDescending(issue => issue.UpdatedAtUtc);
        }

        IOrderedQueryable<Issue>? ordered = null;
        foreach (var sort in query.Sorts)
        {
            ordered = ApplySort(ordered ?? result, sort, ordered is not null);
        }

        return ordered ?? result;
    }

    private static Expression<Func<Issue, bool>> BuildPredicate(JqlExpression expression, JqlTranslationContext context)
    {
        var parameter = Expression.Parameter(typeof(Issue), "issue");
        var body = BuildExpression(expression, context, parameter);
        return Expression.Lambda<Func<Issue, bool>>(body, parameter);
    }

    private static Expression BuildExpression(JqlExpression expression, JqlTranslationContext context, ParameterExpression parameter)
    {
        return expression switch
        {
            JqlBinaryExpression binary => binary.Operator == JqlLogicalOperator.And
                ? Expression.AndAlso(BuildExpression(binary.Left, context, parameter), BuildExpression(binary.Right, context, parameter))
                : Expression.OrElse(BuildExpression(binary.Left, context, parameter), BuildExpression(binary.Right, context, parameter)),
            JqlCondition condition => BuildCondition(condition, context, parameter),
            _ => throw new InvalidOperationException("Unsupported JQL expression.")
        };
    }

    private static Expression BuildCondition(JqlCondition condition, JqlTranslationContext context, ParameterExpression parameter)
    {
        var field = NormalizeField(condition.Field);
        return field switch
        {
            "project" => BuildProjectCondition(condition, parameter),
            "status" => BuildStringComparison(condition, Property(parameter, nameof(Issue.WorkflowStatus), nameof(WorkflowStatus.Name)), parameter),
            "type" => BuildEnumComparison<IssueType>(condition, Property(parameter, nameof(Issue.Type)), parameter),
            "priority" => BuildEnumComparison<IssuePriority>(condition, Property(parameter, nameof(Issue.Priority)), parameter),
            "assignee" => BuildAssigneeCondition(condition, context, parameter),
            "reporter" => BuildReporterCondition(condition, context, parameter),
            "sprint" => BuildStringComparison(condition, Property(parameter, nameof(Issue.Sprint), nameof(Sprint.Name)), parameter),
            "created" => BuildDateTimeComparison(condition, Property(parameter, nameof(Issue.CreatedAtUtc)), context),
            "updated" => BuildDateTimeComparison(condition, Property(parameter, nameof(Issue.UpdatedAtUtc)), context),
            "duedate" => BuildDateOnlyComparison(condition, Property(parameter, nameof(Issue.DueDate)), context),
            "storypoints" => BuildNullableIntComparison(condition, Property(parameter, nameof(Issue.StoryPoints))),
            "label" => BuildCollectionStringCondition(condition, parameter, nameof(Issue.IssueLabels), nameof(IssueLabel.Label), nameof(Label.Name)),
            "component" => BuildCollectionStringCondition(condition, parameter, nameof(Issue.IssueComponents), nameof(IssueComponent.Component), nameof(Component.Name)),
            _ => throw new InvalidOperationException($"The field '{condition.Field}' is not supported in JQL.")
        };
    }

    private static string NormalizeField(string value) => (value ?? string.Empty).Replace("_", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();

    private static Expression BuildProjectCondition(JqlCondition condition, ParameterExpression parameter)
    {
        var keyExpression = Property(parameter, nameof(Issue.Project), nameof(Project.Key));
        var nameExpression = Property(parameter, nameof(Issue.Project), nameof(Project.Name));
        return CombineWithOperator(condition, condition.Values.Select(value =>
        {
            var normalized = NormalizeStringValue(value);
            return Expression.OrElse(BuildStringEquals(keyExpression, normalized), BuildStringEquals(nameExpression, normalized));
        }).ToList());
    }

    private static Expression BuildStringComparison(JqlCondition condition, Expression propertyExpression, ParameterExpression parameter)
    {
        return condition.Operator switch
        {
            JqlComparisonOperator.Equals or JqlComparisonOperator.In => CombineWithOperator(condition, condition.Values.Select(value => BuildStringEquals(propertyExpression, NormalizeStringValue(value))).ToList()),
            JqlComparisonOperator.NotEquals or JqlComparisonOperator.NotIn => Expression.Not(CombineWithOperator(new JqlCondition(condition.Field, condition.Operator is JqlComparisonOperator.NotEquals ? JqlComparisonOperator.Equals : JqlComparisonOperator.In, condition.Values), condition.Values.Select(value => BuildStringEquals(propertyExpression, NormalizeStringValue(value))).ToList())),
            _ => throw new InvalidOperationException($"Operator '{condition.Operator}' is not valid for field '{condition.Field}'.")
        };
    }

    private static Expression BuildEnumComparison<TEnum>(JqlCondition condition, Expression propertyExpression, ParameterExpression parameter)
        where TEnum : struct, Enum
    {
        var values = condition.Values.Select(value =>
        {
            var normalized = NormalizeStringValue(value).Replace(" ", string.Empty, StringComparison.Ordinal);
            if (!Enum.TryParse<TEnum>(normalized, true, out var parsed))
            {
                throw new InvalidOperationException($"'{normalized}' is not a valid {typeof(TEnum).Name} value.");
            }

            return Expression.Equal(propertyExpression, Expression.Constant(parsed));
        }).ToList();

        return condition.Operator switch
        {
            JqlComparisonOperator.Equals or JqlComparisonOperator.In => CombineWithOperator(condition, values),
            JqlComparisonOperator.NotEquals or JqlComparisonOperator.NotIn => Expression.Not(CombineWithOperator(new JqlCondition(condition.Field, condition.Operator is JqlComparisonOperator.NotEquals ? JqlComparisonOperator.Equals : JqlComparisonOperator.In, condition.Values), values)),
            _ => throw new InvalidOperationException($"Operator '{condition.Operator}' is not valid for field '{condition.Field}'.")
        };
    }

    private static Expression BuildAssigneeCondition(JqlCondition condition, JqlTranslationContext context, ParameterExpression parameter)
    {
        var collection = Property(parameter, nameof(Issue.Assignees));
        var item = Expression.Parameter(typeof(IssueAssignee), "assignee");
        var userId = Property(item, nameof(IssueAssignee.UserId));
        var userName = Property(item, nameof(IssueAssignee.User), nameof(User.UserName));
        var displayName = Property(item, nameof(IssueAssignee.User), nameof(User.DisplayName));
        var comparisons = condition.Values.Select(value => BuildUserMatch(value, context, userId, userName, displayName)).ToList();
        var predicate = Expression.Lambda<Func<IssueAssignee, bool>>(CombineWithOperator(condition, comparisons), item);
        var anyMethod = typeof(Enumerable).GetMethods().First(x => x.Name == nameof(Enumerable.Any) && x.GetParameters().Length == 2).MakeGenericMethod(typeof(IssueAssignee));
        var any = Expression.Call(anyMethod, collection, predicate);
        return condition.Operator is JqlComparisonOperator.NotEquals or JqlComparisonOperator.NotIn ? Expression.Not(any) : any;
    }

    private static Expression BuildReporterCondition(JqlCondition condition, JqlTranslationContext context, ParameterExpression parameter)
    {
        var reporterId = Property(parameter, nameof(Issue.ReporterId));
        var userName = Property(parameter, nameof(Issue.Reporter), nameof(User.UserName));
        var displayName = Property(parameter, nameof(Issue.Reporter), nameof(User.DisplayName));
        var comparisons = condition.Values.Select(value => BuildUserMatch(value, context, reporterId, userName, displayName)).ToList();
        var combined = CombineWithOperator(condition, comparisons);
        return condition.Operator is JqlComparisonOperator.NotEquals or JqlComparisonOperator.NotIn ? Expression.Not(combined) : combined;
    }

    private static Expression BuildUserMatch(JqlValue value, JqlTranslationContext context, Expression idExpression, Expression userNameExpression, Expression displayNameExpression)
    {
        if (value is JqlFunctionValue function && string.Equals(function.Name, "currentUser", StringComparison.OrdinalIgnoreCase))
        {
            return Expression.Equal(idExpression, Expression.Constant(context.CurrentUserId));
        }

        var normalized = NormalizeStringValue(value);
        return Expression.OrElse(BuildStringEquals(userNameExpression, normalized), BuildStringEquals(displayNameExpression, normalized));
    }

    private static Expression BuildDateTimeComparison(JqlCondition condition, Expression propertyExpression, JqlTranslationContext context)
    {
        var value = ResolveDateTimeValue(condition.Values.Single(), context);
        var constant = Expression.Constant(value, typeof(DateTime));
        return condition.Operator switch
        {
            JqlComparisonOperator.Equals => Expression.Equal(propertyExpression, constant),
            JqlComparisonOperator.NotEquals => Expression.NotEqual(propertyExpression, constant),
            JqlComparisonOperator.GreaterThan => Expression.GreaterThan(propertyExpression, constant),
            JqlComparisonOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(propertyExpression, constant),
            JqlComparisonOperator.LessThan => Expression.LessThan(propertyExpression, constant),
            JqlComparisonOperator.LessThanOrEqual => Expression.LessThanOrEqual(propertyExpression, constant),
            _ => throw new InvalidOperationException($"Operator '{condition.Operator}' is not valid for field '{condition.Field}'.")
        };
    }

    private static Expression BuildDateOnlyComparison(JqlCondition condition, Expression propertyExpression, JqlTranslationContext context)
    {
        var value = ResolveDateOnlyValue(condition.Values.Single(), context);
        var hasValue = Expression.Property(propertyExpression, nameof(Nullable<DateOnly>.HasValue));
        var actual = Expression.Property(propertyExpression, nameof(Nullable<DateOnly>.Value));
        var constant = Expression.Constant(value, typeof(DateOnly));
        var comparison = condition.Operator switch
        {
            JqlComparisonOperator.Equals => Expression.Equal(actual, constant),
            JqlComparisonOperator.NotEquals => Expression.NotEqual(actual, constant),
            JqlComparisonOperator.GreaterThan => Expression.GreaterThan(actual, constant),
            JqlComparisonOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(actual, constant),
            JqlComparisonOperator.LessThan => Expression.LessThan(actual, constant),
            JqlComparisonOperator.LessThanOrEqual => Expression.LessThanOrEqual(actual, constant),
            _ => throw new InvalidOperationException($"Operator '{condition.Operator}' is not valid for field '{condition.Field}'.")
        };

        return condition.Operator == JqlComparisonOperator.NotEquals
            ? Expression.OrElse(Expression.Not(hasValue), comparison)
            : Expression.AndAlso(hasValue, comparison);
    }

    private static Expression BuildNullableIntComparison(JqlCondition condition, Expression propertyExpression)
    {
        var valueToken = condition.Values.Single();
        if (valueToken is not JqlNumberValue numberValue)
        {
            throw new InvalidOperationException("storyPoints requires a numeric value.");
        }

        var constant = Expression.Constant((int)numberValue.Value, typeof(int?));
        return condition.Operator switch
        {
            JqlComparisonOperator.Equals => Expression.Equal(propertyExpression, constant),
            JqlComparisonOperator.NotEquals => Expression.NotEqual(propertyExpression, constant),
            JqlComparisonOperator.GreaterThan => Expression.GreaterThan(propertyExpression, constant),
            JqlComparisonOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(propertyExpression, constant),
            JqlComparisonOperator.LessThan => Expression.LessThan(propertyExpression, constant),
            JqlComparisonOperator.LessThanOrEqual => Expression.LessThanOrEqual(propertyExpression, constant),
            _ => throw new InvalidOperationException($"Operator '{condition.Operator}' is not valid for field '{condition.Field}'.")
        };
    }

    private static Expression BuildCollectionStringCondition(JqlCondition condition, ParameterExpression parameter, string collectionName, string navigationName, string valueName)
    {
        var collection = Property(parameter, collectionName);
        var itemType = collection.Type.GetGenericArguments()[0];
        var item = Expression.Parameter(itemType, "item");
        var property = Property(item, navigationName, valueName);
        var comparisons = condition.Values.Select(value => BuildStringEquals(property, NormalizeStringValue(value))).ToList();
        var predicateBody = CombineWithOperator(condition, comparisons);
        var predicate = Expression.Lambda(predicateBody, item);
        var anyMethod = typeof(Enumerable).GetMethods().First(x => x.Name == nameof(Enumerable.Any) && x.GetParameters().Length == 2).MakeGenericMethod(itemType);
        var any = Expression.Call(anyMethod, collection, predicate);
        return condition.Operator is JqlComparisonOperator.NotEquals or JqlComparisonOperator.NotIn ? Expression.Not(any) : any;
    }

    private static Expression CombineWithOperator(JqlCondition condition, IReadOnlyList<Expression> comparisons)
    {
        if (comparisons.Count == 0)
        {
            return Expression.Constant(true);
        }

        var aggregate = comparisons[0];
        for (var index = 1; index < comparisons.Count; index++)
        {
            aggregate = condition.Operator is JqlComparisonOperator.Equals or JqlComparisonOperator.In
                ? Expression.OrElse(aggregate, comparisons[index])
                : Expression.OrElse(aggregate, comparisons[index]);
        }

        return aggregate;
    }

    private static string NormalizeStringValue(JqlValue value) => value switch
    {
        JqlStringValue stringValue => stringValue.Value.Trim(),
        JqlNumberValue numberValue => numberValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        JqlFunctionValue functionValue => functionValue.Name.Trim(),
        _ => throw new InvalidOperationException("A string-compatible JQL value was expected.")
    };

    private static DateTime ResolveDateTimeValue(JqlValue value, JqlTranslationContext context)
    {
        return value switch
        {
            JqlRelativeDateValue relative => relative.Unit switch
            {
                'd' => context.NowUtc.Date.AddDays(relative.Amount),
                'w' => context.NowUtc.Date.AddDays(relative.Amount * 7),
                'm' => context.NowUtc.Date.AddMonths(relative.Amount),
                _ => throw new InvalidOperationException("Relative date unit must be d, w, or m.")
            },
            JqlStringValue stringValue when DateTime.TryParse(stringValue.Value, out var parsed) => parsed.ToUniversalTime(),
            _ => throw new InvalidOperationException("A valid date value was expected.")
        };
    }

    private static DateOnly ResolveDateOnlyValue(JqlValue value, JqlTranslationContext context)
    {
        return value switch
        {
            JqlRelativeDateValue => DateOnly.FromDateTime(ResolveDateTimeValue(value, context)),
            JqlStringValue stringValue when DateOnly.TryParse(stringValue.Value, out var parsed) => parsed,
            JqlStringValue stringValue when DateTime.TryParse(stringValue.Value, out var parsedDateTime) => DateOnly.FromDateTime(parsedDateTime),
            _ => throw new InvalidOperationException("A valid date value was expected.")
        };
    }

    private static Expression Property(Expression source, params string[] names)
    {
        Expression current = source;
        foreach (var name in names)
        {
            current = Expression.Property(current, name);
        }

        return current;
    }

    private static Expression BuildStringEquals(Expression propertyExpression, string value)
    {
        var coalesced = propertyExpression.Type == typeof(string)
            ? Expression.Coalesce(propertyExpression, Expression.Constant(string.Empty))
            : propertyExpression;
        var toUpper = Expression.Call(coalesced, typeof(string).GetMethod(nameof(string.ToUpper), Type.EmptyTypes)!);
        return Expression.Equal(toUpper, Expression.Constant(value.Trim().ToUpperInvariant()));
    }

    private static IOrderedQueryable<Issue> ApplySort(IQueryable<Issue> source, JqlSortClause sort, bool append)
    {
        var field = NormalizeField(sort.Field);
        return field switch
        {
            "project" => OrderBy(source, issue => issue.Project.Key, sort.Descending, append),
            "status" => OrderBy(source, issue => issue.WorkflowStatus.DisplayOrder, sort.Descending, append),
            "type" => OrderBy(source, issue => issue.Type, sort.Descending, append),
            "priority" => OrderBy(source, issue => issue.Priority, sort.Descending, append),
            "assignee" => OrderBy(source, issue => issue.Assignees.OrderBy(assignee => assignee.User.DisplayName).Select(assignee => assignee.User.DisplayName).FirstOrDefault() ?? string.Empty, sort.Descending, append),
            "reporter" => OrderBy(source, issue => issue.Reporter.DisplayName, sort.Descending, append),
            "sprint" => OrderBy(source, issue => issue.Sprint != null ? issue.Sprint.Name : string.Empty, sort.Descending, append),
            "created" => OrderBy(source, issue => issue.CreatedAtUtc, sort.Descending, append),
            "updated" => OrderBy(source, issue => issue.UpdatedAtUtc, sort.Descending, append),
            "duedate" => OrderBy(source, issue => issue.DueDate, sort.Descending, append),
            "storypoints" => OrderBy(source, issue => issue.StoryPoints, sort.Descending, append),
            "label" => OrderBy(source, issue => issue.IssueLabels.OrderBy(label => label.Label.Name).Select(label => label.Label.Name).FirstOrDefault() ?? string.Empty, sort.Descending, append),
            "component" => OrderBy(source, issue => issue.IssueComponents.OrderBy(component => component.Component.Name).Select(component => component.Component.Name).FirstOrDefault() ?? string.Empty, sort.Descending, append),
            _ => throw new InvalidOperationException($"The field '{sort.Field}' is not supported in ORDER BY.")
        };
    }

    private static IOrderedQueryable<Issue> OrderBy<TKey>(IQueryable<Issue> source, Expression<Func<Issue, TKey>> keySelector, bool descending, bool append)
    {
        if (append && source is IOrderedQueryable<Issue> ordered)
        {
            return descending ? ordered.ThenByDescending(keySelector) : ordered.ThenBy(keySelector);
        }

        return descending ? source.OrderByDescending(keySelector) : source.OrderBy(keySelector);
    }
}
