# MIGRATION AUDIT

**Date:** 2026-04-01  
**Project:** Jira Clone Desktop  
**Stack:** .NET 8 WinForms + EF Core 8 + SQL Server  
**Goal:** Restore migration hygiene and verify clean install path from zero.

## 1. Migration Inventory

| Migration Name | .cs | .Designer.cs | .resx | Status |
| --- | --- | --- | --- | --- |
| 20260310102420_InitialCreate | Yes | Yes | No | OK |
| 20260310103544_CommentAndAttachmentSoftDelete | Yes | Yes | No | OK |
| 20260310164648_IssueSoftDelete | Yes | Yes | No | OK |
| 20260313224500_SprintSingleActiveConstraint | Yes | Yes | No | OK |
| 20260317142208_AddParentIssueAndEpicType | Yes | Yes | No | OK |
| 20260317162311_AddLabelsComponentsVersions | Yes | Yes | No | OK |
| 20260318022750_AddWorkflowEngine | Yes | Yes | No | OK |
| 20260318024221_AddSavedFilters | Yes | Yes | No | OK |
| 20260318053100_AddSprintSoftDeleteForProjectDelete | Yes | Yes | No | OK |
| 20260318060215_AddBoardTypeForKanban | Yes | Yes | No | OK |
| 20260318090723_AddWatchersAndNotifications | Yes | Yes | No | OK |
| 20260318125155_AddPermissionScheme | Yes | Yes | No | OK |
| 20260318151106_AddIssueStartDateForRoadmap | Yes | Yes | No | OK |
| 20260318154208_AddWebhooks | Yes | Yes | No | OK |
| 20260318165615_AddProjectIntegrations | Yes | Yes | No | OK |
| 20260318170000_AddEmailNotificationPreference | Yes | Yes | No | OK |
| 20260318172630_AddApiTokens | Yes | Yes | No | OK |
| 20260318183000_AddRememberMeSessionPersistence | Yes | Yes | No | OK |
| 20260320110000_ResetSeedPasswordsToChangeMe123 | Yes | Yes | No | OK |

## 2. Missing Designer Files Fixed

The following migrations were missing their corresponding `.Designer.cs` files and were reconstructed so the migration folder is now structurally complete:

- `20260313224500_SprintSingleActiveConstraint`
- `20260318170000_AddEmailNotificationPreference`
- `20260318183000_AddRememberMeSessionPersistence`
- `20260320110000_ResetSeedPasswordsToChangeMe123`

Notes:

- No migration in this project uses a `.resx` resource file.
- Existing historical drift was already present before this cleanup. In particular, `20260318165615_AddProjectIntegrations.Designer.cs` already contained later `User` fields (`EmailNotificationsEnabled`, `LastRefreshToken`, `SessionExpiresAtUtc`). This audit preserved the current migration chain and focused on restoring a working clean-install path.

## 3. Design-Time EF Fix

`dotnet ef` was initially failing to recreate the database after the secrets cleanup because EF was resolving the placeholder connection string from committed config instead of the real local secret.

Root cause:

- EF tooling was using `JiraCloneDbContextFactory`.
- That factory needed to load `appsettings.Local.json` and resolve the WinForms startup project path robustly.

Current status:

- `src/JiraClone.Persistence/JiraCloneDbContextFactory.cs` now loads:
  1. `appsettings.json`
  2. `appsettings.{Environment}.json`
  3. `appsettings.Local.json`
  4. `JIRACLONE_` environment variables
- `dotnet ef dbcontext info` now succeeds and reports:
  - factory: `JiraCloneDbContextFactory`
  - context: `JiraCloneDbContext`
  - database: `JiraCloneWinForms`
  - server: `DESKTOP-ISSK39T`

## 4. Clean Install Verification

Executed successfully:

```powershell
dotnet build src/JiraClone.WinForms/JiraClone.WinForms.csproj
dotnet ef database drop --project src/JiraClone.Persistence/JiraClone.Persistence.csproj --startup-project src/JiraClone.WinForms/JiraClone.WinForms.csproj --context JiraCloneDbContext --force
dotnet ef database update --project src/JiraClone.Persistence/JiraClone.Persistence.csproj --startup-project src/JiraClone.WinForms/JiraClone.WinForms.csproj --context JiraCloneDbContext
dotnet test tests/JiraClone.Tests/JiraClone.Tests.csproj
```

Observed result:

- Build: passed, `0 Warning(s)`, `0 Error(s)`
- Database drop: succeeded
- Database update: succeeded from `InitialCreate` through `ResetSeedPasswordsToChangeMe123`
- Test suite: `105/105` passed

One warning remains during `database update`:

- EF warns that `Project.BoardType` has a database default with no sentinel value. This does not block migration execution, but it is worth tracking before release.

## 5. Business Constraint Verification

### 5.1 Seed data loaded correctly

Verified on the recreated database with a temporary probe against the real SQL Server database:

- `LOGIN_SUCCESS=True;LOGIN_USER=admin`
- `ROLE_COUNT=4`
- `USER_COUNT=3`
- `PROJECT_COUNT=1`
- `ACTIVE_SPRINT_COUNT=1`

Interpretation:

- Seed roles are present
- Seed users are present
- Seed project is present
- Seed credential `admin / ChangeMe123!` authenticates successfully
- One active sprint exists in the seeded project as expected

### 5.2 Unique active sprint constraint does not block project archive

Verified with the same probe while the seeded project still had one active sprint:

- `ARCHIVE_RESULT=True;PROJECT_ACTIVE_AFTER_ARCHIVE=False`

Interpretation:

- The `SprintSingleActiveConstraint` filtered unique index does not interfere with archiving a project.

### 5.3 Soft delete / hard delete path previously failed, now fixed

During verification, a real bug was found in the project delete path:

- `ProjectCommandService.DeleteProjectAsync` soft-deleted issues/comments/attachments/sprints
- then `ProjectRepository.DeleteAsync` tried to delete the `Project` while the same `DbContext` was still tracking the full issue graph
- EF threw a required-relationship error on `Issue -> IssueLabel` before SQL cascade delete could execute

Fix applied:

- `src/JiraClone.Persistence/Repositories/ProjectRepository.cs`
- `DeleteAsync` now clears the current tracker and deletes a fresh project stub by key:
  - this avoids EF client-side graph reconciliation on previously tracked issue children
  - SQL Server performs the actual cascade delete

Re-verified with the real database after the fix:

- `DELETE_RESULT=True;PROJECT_EXISTS_AFTER_DELETE=False;SOFT_DELETED_ISSUES=0;SOFT_DELETED_COMMENTS=0;SOFT_DELETED_ATTACHMENTS=0;SOFT_DELETED_SPRINTS=0`

Interpretation:

- The delete flow now completes without foreign-key / required-relationship failures.
- Counts are zero after delete because the final hard delete removes the project tree from the database after the soft-delete bookkeeping phase.

## 6. Files Changed In This Migration Hygiene Pass

- `src/JiraClone.Persistence/JiraCloneDbContextFactory.cs`
- `src/JiraClone.Persistence/Repositories/ProjectRepository.cs`
- `src/JiraClone.Persistence/Migrations/20260313224500_SprintSingleActiveConstraint.Designer.cs`
- `src/JiraClone.Persistence/Migrations/20260318170000_AddEmailNotificationPreference.Designer.cs`
- `src/JiraClone.Persistence/Migrations/20260318183000_AddRememberMeSessionPersistence.Designer.cs`
- `src/JiraClone.Persistence/Migrations/20260320110000_ResetSeedPasswordsToChangeMe123.Designer.cs`

## 7. Final State

- All migrations now have `.cs` + `.Designer.cs`
- `dotnet ef` design-time path works again with local externalized secrets
- Clean install from zero succeeds
- Full test suite still passes (`105/105`)
- Seed login was verified against the recreated database
- The local database was recreated again at the end so the workspace is left on a clean seeded state

## 8. Remaining Risks / Follow-ups

- Track the EF warning for `Project.BoardType` sentinel/default behavior before release packaging.
- Consider adding a targeted regression test around project deletion using a relational provider, because the original failure only surfaced when EF tracked a real SQL-backed graph.
- Consider normalizing historical migration designer drift in a future dedicated cleanup, but do not rewrite the chain casually if any shared environment has already consumed it.
