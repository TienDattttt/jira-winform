# Jira Clone ERD

Assumption: the requested `SQL Server 2919` syntax is a typo, so this schema is written in SQL Server 2019/2022-compatible T-SQL.

## Core relationships

- `Users` to `Roles`: many-to-many through `UserRoles`.
- `Projects` to `Users`: many-to-many through `ProjectMembers`.
- `Projects` to `BoardColumns`: one-to-many.
- `Projects` to `Sprints`: one-to-many.
- `Projects` to `Issues`: one-to-many.
- `Issues` to `Users` as `reporter`: many-to-one.
- `Issues` to `Users` as `created_by`: many-to-one.
- `Issues` to `IssueTypes`: many-to-one.
- `Issues` to `IssueStatuses`: many-to-one.
- `Issues` to `Priorities`: many-to-one.
- `Issues` to `Sprints`: many-to-one, optional.
- `Issues` to `Users`: many-to-many through `IssueAssignees`.
- `Issues` to `Comments`: one-to-many.
- `Issues` to `Attachments`: one-to-many.
- `Issues` to `ActivityLogs`: one-to-many.
- `Projects` to `ActivityLogs`: one-to-many.

## Entity summary

- `Roles`: global authorization catalog.
- `Users`: application login and profile records.
- `UserRoles`: global system role assignments.
- `Projects`: top-level workspaces.
- `ProjectMembers`: membership and project-scoped role assignment.
- `IssueTypes`: lookup table for `Task`, `Bug`, `Story`.
- `IssueStatuses`: lookup table for `Backlog`, `Selected`, `In Progress`, `Done`.
- `Priorities`: lookup table for `Lowest` through `Highest`.
- `BoardColumns`: visual board configuration per project, mapped to an issue status.
- `Sprints`: scrum iteration records.
- `Issues`: main work item table.
- `IssueAssignees`: multi-assignee bridge table.
- `Comments`: issue discussion.
- `Attachments`: file metadata stored in SQL Server with files stored externally.
- `ActivityLogs`: audit trail for issue/project changes.

## Soft delete

- Every table includes `is_deleted`.
- Unique indexes that must ignore soft-deleted rows are implemented as filtered unique indexes with `WHERE is_deleted = 0`.
- Physical deletes should be reserved for maintenance only.
