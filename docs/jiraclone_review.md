# JiraClone WinForms — Architecture & Code Review (Updated)

> **Reviewer Role**: Architecture Reviewer, Code Reviewer, QA Engineer  
> **Project Path**: `d:\Ki2Nam4\.Net\jira_clone`  
> **Date**: 2026-03-10 (Re-review)

---

## 1. Feature Assessment Table

### Phase 1 — Foundation, Authentication & Core Data Model

| Feature | Planned Files | Status | Notes |
|---|---|---|---|
| Domain entities (12 total) | `JiraClone.Domain/Entities/*` | ✅ Done | User, Role, Project, ProjectMember, BoardColumn, Issue, Sprint, Comment, Attachment, ActivityLog, IssueAssignee, UserRole — all with correct fields, relationships, RowVersion, soft-delete |
| Enums (7 total) | `JiraClone.Domain/Enums/*` | ✅ Done | IssueStatus, IssueType, IssuePriority, SprintState, ActivityActionType, ProjectCategory, ProjectRole |
| Common base classes | `JiraClone.Domain/Common/*` | ✅ Done | AggregateRoot, AuditableEntity with Id/CreatedAtUtc/UpdatedAtUtc |
| Application abstractions (12 total) | `JiraClone.Application/Abstractions/*` | ✅ Done | All 11 plan ports + new `IAuthorizationService` |
| Auth service | `AuthenticationService.cs` | ✅ Done | Login with password hash/salt, sets CurrentUserContext |
| EF Core DbContext | `JiraCloneDbContext.cs` | ✅ Done | 12 DbSets, configurations from assembly, SeedData |
| EF Configurations (12) | `Configurations/*` | ✅ Done | All entity configurations |
| EF Migrations | `Migrations/*` | ✅ Done | InitialCreate + CommentAndAttachmentSoftDelete |
| Seed data | `SeedData.cs` | ✅ Done | 209L: roles, users, project, columns, sprint, issues, assignees, comments, activity |
| Infrastructure security | `Security/*` | ✅ Done | Sha256PasswordHasher, CurrentUserContext |
| Program.cs | `Program.cs` | ✅ Done | LocalDB, auto-migrate, attachment root |
| LoginForm | `LoginForm.cs` | ✅ Done | 68L. Full auth flow with error handling |
| MainForm | `MainForm.cs` | ✅ Done | 20L. TabControl: Board/Sprints/Users/Project Settings |

### Phase 2 — Issue Management & Board Parity

| Feature | Planned Files | Status | Notes |
|---|---|---|---|
| IssueService (CRUD + Move) | `IssueService.cs` | ✅ Done | 203L. Create/Update/Move/Delete/GetDetails + activity logging + **RBAC injected** |
| BoardQueryService | `BoardQueryService.cs` | ✅ Done | 54L. 4 columns, sprint filtering |
| DTOs (4) | `Models/*` | ✅ Done | IssueSummaryDto, BoardColumnDto, IssueEditModel, IssueDetailsDto |
| BoardForm | `BoardForm.cs` | ✅ Done | 210L. Columns, filters (assignee/priority/type/search), sprint toggle |
| IssueEditorForm | `IssueEditorForm.cs` | ✅ Done | 157L. All fields + multi-assignee + sprint selector |
| IssueDetailsForm | `IssueDetailsForm.cs` | ✅ Done | 222L. Tabs: Comments/Attachments/Activity, full CRUD |
| IssueCardControl | `IssueCardControl.cs` | ✅ Done | 40L. Clickable card |
| BoardColumnControl | `BoardColumnControl.cs` | ✅ Done | 54L. Column panel with click events |
| ViewModels | `ViewModels/*` | ⚠️ Partial | `BoardViewModel.cs` (10L) exists but **unused** — BoardForm uses DTOs directly |
| IssueRepository | `IssueRepository.cs` | ✅ Done | 83L. Full CRUD with eager loading |

### Phase 3 — Comments, Activity Log & Attachments

| Feature | Planned Files | Status | Notes |
|---|---|---|---|
| CommentService | `CommentService.cs` | ✅ Done | 97L. Add/Update/SoftDelete + activity logging + **RBAC injected** |
| AttachmentFacade | `AttachmentFacade.cs` | ✅ Done | 83L. Add/GetByIssue/Download/SoftDelete + activity logging + **RBAC injected** |
| ActivityLogService | `ActivityLogService.cs` | ✅ Done | 18L. Query wrapper |
| FileShareAttachmentService | `FileShareAttachmentService.cs` | ✅ Done | 64L. Save/Delete/ResolvePath + SHA-256 checksum |
| CommentListControl | `CommentListControl.cs` | ✅ Done | 79L. ListView with edit/delete |
| AttachmentListControl | `AttachmentListControl.cs` | ✅ Done | 75L. ListView with download/delete |
| AttachmentPicker | `AttachmentPicker.cs` | ✅ Done | 86L. Browse/Upload + extension/size validation |
| ActivityTimelineControl | `ActivityTimelineControl.cs` | ✅ Done | 23L. ListBox timeline |
| Repositories (3) | `CommentRepository.cs`, `AttachmentRepository.cs`, `ActivityLogRepository.cs` | ✅ Done | |

### Phase 4 — Sprint Management

| Feature | Planned Files | Status | Notes |
|---|---|---|---|
| SprintService | `SprintService.cs` | ✅ Done | 144L. Create/Start/Close + incomplete issue handling + **RBAC injected** |
| SprintManagementForm | `SprintManagementForm.cs` | ✅ Done | 241L. Create/Assign/Start/Close + embedded CloseSprintDialog |
| AssignToSprintDialog | `AssignToSprintDialog.cs` | ✅ Done | 66L. Multi-select checklist |
| SprintSelectorControl | `SprintSelectorControl.cs` | ✅ Done | 19L. ComboBox binding |
| SprintRepository | `SprintRepository.cs` | ✅ Done | |

### Phase 5 — User/Role Administration & Production Hardening

| Feature | Planned Files | Status | Notes |
|---|---|---|---|
| IAuthorizationService | `Abstractions/IAuthorizationService.cs` | ✅ Done | **NEW.** IsInRole/EnsureInRole interface |
| AuthorizationService | `Roles/AuthorizationService.cs` | ✅ Done | **NEW.** 33L. Checks UserRoles against required roles |
| UserCommandService | `Users/UserCommandService.cs` | ✅ Done | **NEW.** 175L. Create/Update/ResetPassword/Activate/Deactivate — all Admin-guarded |
| UserQueryService | `Users/UserQueryService.cs` | ✅ Done | 18L. GetProjectUsersAsync |
| ProjectCommandService | `Projects/ProjectCommandService.cs` | ✅ Done | **NEW.** 115L. UpdateProject/AddMember/RemoveMember/UpdateMemberRole/UpdateBoardColumn — Admin+PM guarded |
| RoleCatalog | `Roles/RoleCatalog.cs` | ✅ Done | Admin/ProjectManager/Developer/Viewer constants |
| UserManagementForm | `UserManagementForm.cs` | ✅ Done | **EXPANDED.** 264L (was 33L). Full CRUD: Create/Edit/Deactivate/Activate/ResetPassword + embedded `UserEditorDialog` with role assignment. Buttons disabled for non-Admin |
| ProjectSettingsForm | `ProjectSettingsForm.cs` | ✅ Done | **EXPANDED.** 347L (was 15L stub). 3 tabs: General (edit project), Members (add/remove/change role), Board Columns (edit name/WIP). 3 embedded dialogs: MemberDialog, MemberRoleDialog, BoardColumnDialog. Buttons disabled for non-Admin/PM |
| ErrorDialogService | `ErrorDialogService.cs` | ✅ Done | 11L. Exception + string dialogs |
| Permission enforcement across commands | All services | ✅ Done | **NEW.** `IAuthorizationService` injected into IssueService, CommentService, SprintService, AttachmentFacade, UserCommandService, ProjectCommandService. UI buttons conditionally disabled |
| Password reset workflow | `UserCommandService.ResetPasswordAsync` + `UserManagementForm.ResetPasswordAsync` | ✅ Done | **NEW.** |
| Expanded repositories | `UserRepository` (53L), `ProjectRepository` (34L) | ✅ Done | **EXPANDED.** New: GetAllAsync, GetRolesAsync, AddAsync, GetByIdAsync with eager loading |
| Structured logging / diagnostics | Not created | ❌ Missing | No ILogger integration |
| Installer / publishing setup | Not created | ❌ Missing | No publish profile |
| Import scripts (PostgreSQL migration) | Not created | ❌ Missing | No data migration tooling |

### Test Plan

| Feature | Planned | Status | Notes |
|---|---|---|---|
| Domain tests | `JiraClone.Tests/Domain/*` | ⚠️ Partial | 1 test: `MoveTo_Updates_Status_And_Position` |
| Application tests | `JiraClone.Tests/Application/*` | ⚠️ Partial | 1 test: `RoleCatalog_Contains_Expected_Default_Roles` |
| Persistence tests | None | ❌ Missing | No integration tests |
| WinForms integration tests | None | ❌ Missing | No UI tests |

---

## 2. Files NOT in Plan (Extra Work)

| File | Assessment |
|---|---|
| `AppSession.cs` (Composition root) | ✅ Valuable — manual DI wiring, now includes all new services |
| `IssueSummaryDto.cs`, `AuthResult.cs` | ✅ Necessary DTOs |
| `JiraCloneDbContextFactory.cs` | ✅ Required for EF Migrations tooling |
| `Schema/*` (3 files) | ⚠️ Appears to be dead code — not referenced by main codebase |
| `CloseSprintDialog` (nested class) | ✅ Good UX |
| `UserEditorDialog` (nested in UserManagementForm) | ✅ Necessary for user CRUD |
| `MemberDialog`, `MemberRoleDialog`, `BoardColumnDialog` (nested in ProjectSettingsForm) | ✅ Necessary for project settings |

---

## 3. Prioritized Next Steps

### 🔴 Priority 1 — Complete RBAC Enforcement in Services

1. **Actually call `EnsureInRole` inside IssueService/CommentService/SprintService/AttachmentFacade** — The `_authorization` field is injected but `EnsureInRole` calls may not be present in all mutation methods (Create/Update/Delete). Verify and add where missing. Viewer role should be read-only.

### 🟡 Priority 2 — Test Coverage (Critical Gap)

2. **Domain tests** — Sprint state transitions, board position logic, Issue.MoveTo edge cases
3. **Application service tests** — IssueService CRUD, CommentService CRUD, SprintService lifecycle, AuthorizationService role checks, UserCommandService with mocked repos
4. **Persistence integration tests** — Cascade delete, RowVersion concurrency, unique index enforcement

### 🟢 Priority 3 — Production Hardening

5. **Structured logging** — `ILogger<T>` via `Microsoft.Extensions.Logging`
6. **Diagnostics** — Connection health check, migration status
7. **Installer/publish** — ClickOnce or MSIX
8. **PostgreSQL import scripts** — Optional

---

## 4. Estimated Completion

| Phase | Weight | Completion | Weighted |
|---|---|---|---|
| Phase 1: Foundation & Auth | 20% | **100%** | 20% |
| Phase 2: Issue Management & Board | 25% | **97%** | 24.25% |
| Phase 3: Comments, Activity, Attachments | 20% | **100%** | 20% |
| Phase 4: Sprint Management | 15% | **100%** | 15% |
| Phase 5: Admin & Hardening | 10% | **75%** | 7.5% |
| Tests | 10% | **5%** | 0.5% |
| **Total** | | | **~87%** |

> **Previous review**: ~82%. **Gained +5%** from IAuthorizationService, UserCommandService, ProjectCommandService, UserManagementForm full CRUD, ProjectSettingsForm full implementation.

---

## 5. Architectural Observations

### ✅ Strengths

- Clean Architecture boundaries fully respected
- **RBAC now wired end-to-end**: `IAuthorizationService` injected into all services, UI buttons conditionally disabled based on role
- Consistent async/await pattern throughout
- Activity logging on every mutation
- Soft delete + RowVersion for concurrency
- Comprehensive seed data for immediate demo
- Sprint close handles incomplete issues correctly
- UserCommandService properly hashes passwords for new users
- ProjectSettingsForm has proper member, role, and board column management

### ⚠️ Remaining Concerns

1. **Verify EnsureInRole calls in mutation methods** — `_authorization` is injected but confirm each Create/Update/Delete actually calls `EnsureInRole` (especially in `IssueService`, `CommentService`, `SprintService`)
2. **IssueKey generation** — `DateTime.UtcNow.Ticks % 1000000` will collide. Use a sequential counter per project
3. **No time tracking fields on IssueEditorForm** — EstimateHours/TimeSpentHours/TimeRemainingHours not exposed in UI
4. **StoryPoints and DueDate** not exposed in IssueEditorForm
5. **`InputBox` from Microsoft.VisualBasic** used for comments and password reset — should use proper dialogs
6. **Single DbContext lifetime** per AppSession — may cause stale data in long sessions
7. **No drag-and-drop on board** — uses click-to-edit approach (acceptable per plan's fallback option)
8. **Schema folder** (`Persistence/Schema/*`) appears to be dead code
9. **Tests are critically thin** — only 2 unit tests for the entire project
