# Jira Desktop - Project Audit Report

> **Audit Date:** 2026-03-17  
> **Codebase:** `d:\Ki2Nam4\.Net\jira_clone` (WinForms C# / .NET)  
> **Auditors:** pm-specialist, backend-specialist, frontend-specialist, debugger, qa-engineer

---

## Executive Summary

The Jira Desktop clone is a **well-architected WinForms application** following **Clean Architecture** with 5 separate projects: `Domain`, `Application`, `Infrastructure`, `Persistence`, and `WinForms`. It uses **EF Core** with SQL Server and implements a **Repository + Unit of Work** pattern.

**Current completion: ~35-40%** of core Jira Web features are implemented.

### Strengths
- ✅ Clean Architecture separation (Domain → Application → Infrastructure/Persistence → WinForms)
- ✅ Role-based authorization (Admin / ProjectManager / Developer / Viewer)
- ✅ Comprehensive activity logging for audit trails
- ✅ Concurrency control with `RowVersion` on Issues and Comments
- ✅ Soft-delete pattern for Issues, Comments, and Attachments
- ✅ File-based attachment storage with SHA-256 checksum
- ✅ Thread-safe `SemaphoreSlim`-based serialization for DB operations
- ✅ 17 well-structured unit & integration tests
- ✅ Polished Jira-like UI theme with custom controls

### Critical Gaps
- ❌ No Epic or Subtask issue types (only Task/Bug/Story)
- ❌ No custom workflow engine (hardcoded 4-status pipeline)
- ❌ No JQL or advanced search
- ❌ No Dashboard, Reports, or Analytics
- ❌ No drag-and-drop on board (context menu only for moving)
- ❌ No notifications/email system
- ❌ No Scrum Board vs Kanban Board distinction

---

## 1. GAP Analysis — Jira Web vs Current Implementation

### Core Modules

| # | Module / Feature | Jira Web | Current Status | Notes |
|---|---|---|---|---|
| 1 | **Authentication — Login** | ✅ | ✅ **Implemented** | Username/password with SHA-256 hashing |
| 2 | **Authentication — SSO / OAuth** | ✅ | ❌ **Missing** | Only local DB auth |
| 3 | **Authentication — API Token** | ✅ | ❌ **Missing** | No API or token system |
| 4 | **Authentication — Session Mgmt** | ✅ | ⚠️ **Partial** | In-memory `CurrentUserContext`, no persistence |
| 5 | **Project — Create** | ✅ | ⚠️ **Partial** | Seed data creates project, no UI for creation |
| 6 | **Project — Edit** | ✅ | ✅ **Implemented** | `ProjectSettingsForm` updates name, desc, category, URL |
| 7 | **Project — Archive / Delete** | ✅ | ❌ **Missing** | No archive/delete functionality |
| 8 | **Project — Multi-project support** | ✅ | ❌ **Missing** | Only "active project" (first project), no project switcher |
| 9 | **Board — Scrum Board** | ✅ | ⚠️ **Partial** | Has sprint-filtered board, but no velocity/burndown |
| 10 | **Board — Kanban Board** | ✅ | ❌ **Missing** | No WIP limit enforcement on board, no Kanban mode toggle |
| 11 | **Board — Backlog View** | ✅ | ✅ **Implemented** | `BoardForm(activeSprintOnly: false)` shows all issues |
| 12 | **Board — Drag & Drop** | ✅ | ❌ **Missing** | Move via context menu only |
| 13 | **Issue — Create** | ✅ | ✅ **Implemented** | `IssueEditorForm` with type, priority, assignee, sprint |
| 14 | **Issue — Edit** | ✅ | ✅ **Implemented** | Inline title edit, field-by-field updates |
| 15 | **Issue — Delete (soft)** | ✅ | ✅ **Implemented** | `IsDeleted` flag with activity logging |
| 16 | **Issue Types — Task** | ✅ | ✅ **Implemented** | — |
| 17 | **Issue Types — Bug** | ✅ | ✅ **Implemented** | — |
| 18 | **Issue Types — Story** | ✅ | ✅ **Implemented** | — |
| 19 | **Issue Types — Epic** | ✅ | ❌ **Missing** | No Epic entity or hierarchy |
| 20 | **Issue Types — Subtask** | ✅ | ❌ **Missing** | No parent-child issue relationship |
| 21 | **Issue Detail — Description** | ✅ | ⚠️ **Partial** | Plain `RichTextBox`, no Markdown/WYSIWYG |
| 22 | **Issue Detail — Attachments** | ✅ | ✅ **Implemented** | Upload, download, soft-delete with checksum |
| 23 | **Issue Detail — Comments** | ✅ | ✅ **Implemented** | Add, edit, soft-delete with activity log |
| 24 | **Issue Detail — Activity Log** | ✅ | ✅ **Implemented** | Created/Updated/Deleted/StatusChanged/Sprint events |
| 25 | **Issue Detail — Watchers** | ✅ | ❌ **Missing** | No watcher entity or notification |
| 26 | **Issue Detail — Assignee** | ✅ | ✅ **Implemented** | Multi-assignee with picker dialog |
| 27 | **Issue Detail — Priority** | ✅ | ✅ **Implemented** | 5 levels: Lowest→Highest |
| 28 | **Issue Detail — Labels** | ✅ | ❌ **Missing** | No Label entity |
| 29 | **Issue Detail — Sprint** | ✅ | ✅ **Implemented** | Sprint selector control |
| 30 | **Issue Detail — Story Points** | ✅ | ✅ **Implemented** | Numeric with 0-100 range |
| 31 | **Issue Detail — Fix Version** | ✅ | ❌ **Missing** | No Version entity |
| 32 | **Issue Detail — Time Tracking** | ✅ | ✅ **Implemented** | Estimate, logged, remaining with visual progress bar |
| 33 | **Issue Detail — Due Date** | ✅ | ⚠️ **Partial** | `DueDate` field in entity but no UI control for it |
| 34 | **Sprint — Create** | ✅ | ✅ **Implemented** | Via `SprintManagementForm` |
| 35 | **Sprint — Start** | ✅ | ✅ **Implemented** | With single-active constraint |
| 36 | **Sprint — Complete / Close** | ✅ | ✅ **Implemented** | Move incomplete issues to next sprint or backlog |
| 37 | **Sprint — Assign Issues** | ✅ | ✅ **Implemented** | Batch assignment via dialog |
| 38 | **Sprint — Velocity** | ✅ | ❌ **Missing** | No velocity calculation |
| 39 | **Epic Management** | ✅ | ❌ **Missing** | No Epic support at all |
| 40 | **Roadmap / Timeline** | ✅ | ❌ **Missing** | — |
| 41 | **Workflow — Custom status** | ✅ | ❌ **Missing** | Hardcoded `IssueStatus` enum: Backlog/Selected/InProgress/Done |
| 42 | **Workflow — Transition rules** | ✅ | ❌ **Missing** | Any status→any status with no rules |
| 43 | **Dashboard & Gadgets** | ✅ | ❌ **Missing** | — |
| 44 | **Reports — Burndown** | ✅ | ❌ **Missing** | — |
| 45 | **Reports — Velocity** | ✅ | ❌ **Missing** | — |
| 46 | **Reports — Cumulative Flow** | ✅ | ❌ **Missing** | — |
| 47 | **Reports — Sprint Report** | ✅ | ❌ **Missing** | — |
| 48 | **User Management** | ✅ | ✅ **Implemented** | `UserManagementForm`: create, edit, activate/deactivate, reset password |
| 49 | **Permission — Roles** | ✅ | ✅ **Implemented** | 4 roles: Admin/PM/Developer/Viewer |
| 50 | **Permission — Project Roles** | ✅ | ✅ **Implemented** | `ProjectMember` with `ProjectRole` enum |
| 51 | **Permission — Permission Scheme** | ✅ | ❌ **Missing** | Hardcoded per-service checks only |
| 52 | **Notification & Email** | ✅ | ❌ **Missing** | — |
| 53 | **Search — Basic** | ✅ | ✅ **Implemented** | Text search across title, key, assignee |
| 54 | **Search — JQL** | ✅ | ❌ **Missing** | No query language parser |
| 55 | **Saved Filters** | ✅ | ❌ **Missing** | — |
| 56 | **Components** | ✅ | ❌ **Missing** | No Component entity |
| 57 | **Labels** | ✅ | ❌ **Missing** | No Label entity |
| 58 | **Versions / Releases** | ✅ | ❌ **Missing** | No Version entity |
| 59 | **Integrations** | ✅ | ❌ **Missing** | No external integrations |
| 60 | **Webhooks** | ✅ | ❌ **Missing** | — |

### Summary Counts

| Status | Count |
|--------|-------|
| ✅ Implemented | 22 |
| ⚠️ Partial | 6 |
| ❌ Missing | 32 |

---

## 2. Architecture & Backend Analysis

### 2.1 Architecture Pattern

```
Solution Layout (Clean Architecture):
├── JiraClone.Domain        → Entities, Enums, Value Objects (no dependencies)
├── JiraClone.Application   → Services, Abstractions, DTOs (depends on Domain)
├── JiraClone.Infrastructure→ Security (Sha256Hasher), Storage (FileShare)
├── JiraClone.Persistence   → EF Core DbContext, Repositories, Migrations, Configs
└── JiraClone.WinForms      → Forms, Controls, Theme, Composition (AppSession)
```

**Pattern:** Clean Architecture + Repository + Unit of Work  
**Data Access:** EF Core 8+ with SQL Server (LocalDB)  
**DI:** Manual composition via `AppSession` (no IoC container like Autofac/Microsoft.Extensions.DI)

### 2.2 Database Schema

**13 Tables via EF Core Configurations:**

| Entity | Key Fields | Relations |
|--------|-----------|-----------|
| `User` | UserName, Email, PasswordHash/Salt, AvatarPath, IsActive | → UserRoles, ProjectMemberships, Comments, AssignedIssues |
| `Role` | Name, Description | → UserRoles |
| `UserRole` | UserId, RoleId | M:N junction |
| `Project` | Key, Name, Description, Category, IsActive | → Members, BoardColumns, Issues, Sprints |
| `ProjectMember` | ProjectId, UserId, ProjectRole | M:N junction |
| `BoardColumn` | ProjectId, Name, StatusCode, DisplayOrder, WipLimit | Per project |
| `Sprint` | ProjectId, Name, Goal, StartDate, EndDate, State, ClosedAtUtc | 1:M with Issues |
| `Issue` | ProjectId, SprintId, IssueKey, Title, DescriptionHtml/Text, Type, Status, Priority, ReporterId, CreatedById, StoryPoints, DueDate, BoardPosition, IsDeleted, RowVersion | → Assignees, Comments, Attachments, ActivityLogs |
| `IssueAssignee` | IssueId, UserId, AssignedAtUtc | M:N junction |
| `Comment` | IssueId, UserId, Body, IsDeleted, RowVersion | 1:M |
| `Attachment` | IssueId, StoredFileName, OriginalFileName, ContentType, FileSizeBytes, StoragePath, ChecksumSha256, IsDeleted | 1:M |
| `ActivityLog` | ProjectId, IssueId, UserId, ActionType, FieldName, OldValue, NewValue, MetadataJson | Audit trail |

### 2.3 Missing Tables / Entities

| Missing Entity | Required For |
|----------------|-------------|
| `Epic` | Epic management, roadmap |
| `Label` | Issue labeling / tagging |
| `Component` | Component assignment |
| `Version` (FixVersion) | Release management |
| `Watcher` | Issue watching / notifications |
| `Filter` (SavedFilter) | JQL saved filters |
| `Notification` | In-app / email notifications |
| `Webhook` | Third-party integrations |
| `WorkflowDefinition` | Custom workflows |
| `WorkflowTransition` | Status transition rules |

### 2.4 Business Logic Issues

| # | Issue | Location | Severity |
|---|-------|----------|----------|
| 1 | **No workflow engine** — `Issue.MoveTo()` accepts any status with no transition validation | `Issue.cs:50-55` | 🔴 High |
| 2 | **Hardcoded 4-status enum** — cannot add custom statuses | `IssueStatus.cs` | 🔴 High |
| 3 | **Only 3 issue types** — Task/Bug/Story, missing Epic/Subtask | `IssueType.cs` | 🔴 High |
| 4 | **No parent-child issue hierarchy** — Issue entity has no `ParentIssueId` | `Issue.cs` | 🟡 Medium |
| 5 | **Issue key generation is not concurrency-safe** — reads all issues then calculates max+1, race condition possible | `IssueService.cs:218-230` | 🟡 Medium |
| 6 | **No DueDate UI** — `DueDate` field exists on entity but not exposed in any form | `Issue.cs:41` | 🟡 Medium |
| 7 | **Board columns have WipLimit** but it's never enforced | `BoardColumn.cs:13`, `BoardQueryService.cs` | 🟡 Medium |
| 8 | **`DescriptionHtml` is always set to same value as `DescriptionText`** — no HTML rendering | `IssueService.cs:59,106` | 🟢 Low |
| 9 | **No velocity calculation** — story points exist but never aggregated per sprint | `SprintService.cs` | 🟡 Medium |
| 10 | **No password complexity validation** — any password accepted | `AuthenticationService.cs` | 🟡 Medium |

### 2.5 Hardcoded Values Found

| Value | Location | Description |
|-------|----------|-------------|
| `"admin"` / `"admin123"` | `LoginForm.cs:47,53` | Pre-filled login credentials |
| `"Server=(localdb)\\MSSQLLocalDB..."` | `Program.cs:17-18` | Default connection string |
| `StoryPoints max = 100` | `IssueDetailsForm.cs:45` | Max story points capped at 100 |
| `BoardPosition default = 1` | `Issue.cs:42` | Fixed initial board position |
| 4 column names | `BoardQueryService.cs:9-15` | Hardcoded column names: Backlog/Selected/In Progress/Done |

---

## 3. UI/UX Analysis — Comparison with Jira Web

### 3.1 Implemented UI Components

| Component | Status | Quality |
|-----------|--------|---------|
| Login form (centered card, shadow, logo) | ✅ | ⭐⭐⭐⭐ Good |
| Sidebar navigation (Board, Backlog, Sprints, Issues, Settings) | ✅ | ⭐⭐⭐⭐ Good |
| Top navbar with search, Create button, avatar | ✅ | ⭐⭐⭐⭐ Good |
| Board columns with issue cards | ✅ | ⭐⭐⭐ Adequate |
| Issue card (type icon, priority icon, avatar, hover shadow) | ✅ | ⭐⭐⭐⭐ Good |
| Issue details split view (left: content, right: details) | ✅ | ⭐⭐⭐⭐ Good |
| Comment list with edit/delete | ✅ | ⭐⭐⭐ Adequate |
| Activity timeline | ✅ | ⭐⭐⭐ Adequate |
| Attachment upload/download/delete | ✅ | ⭐⭐⭐ Adequate |
| Time tracking progress bar | ✅ | ⭐⭐⭐⭐ Good |
| Sprint management form | ✅ | ⭐⭐⭐ Adequate |
| Issue navigator (DataGridView with filters) | ✅ | ⭐⭐⭐ Adequate |
| Project settings form (board columns, members) | ✅ | ⭐⭐⭐ Adequate |
| User management form | ✅ | ⭐⭐⭐ Adequate |
| Quick filters (Assignee, Priority, Type, Search) | ✅ | ⭐⭐⭐⭐ Good |
| Custom theme system (`JiraTheme`, `JiraControlFactory`) | ✅ | ⭐⭐⭐⭐⭐ Excellent |
| Custom icons (`JiraIcons` for types/priorities) | ✅ | ⭐⭐⭐⭐ Good |

### 3.2 Missing UI Components (vs Jira Web)

| Component | Priority | Complexity |
|-----------|----------|------------|
| **Drag & drop on board** (move cards between columns) | 🔴 High | High |
| **Rich text editor** for Description (Markdown/WYSIWYG) | 🔴 High | Medium |
| **Attachment preview** (inline image thumbnails, PDF preview) | 🟡 Medium | Medium |
| **@mention in comments** | 🟡 Medium | Medium |
| **Sprint planning panel** (drag from backlog to sprint) | 🔴 High | High |
| **Backlog view with grouping** (by sprint / by epic) | 🟡 Medium | Medium |
| **Color coding for status** (board column header colors) | 🟢 Low | Low |
| **Loading states** (skeletons / spinners during async ops) | 🟡 Medium | Low |
| **Empty states** (empty board illustration) | 🟢 Low | Low |
| **Keyboard shortcuts** (e.g., `C` to create, `/` to search) | 🟡 Medium | Low |
| **Breadcrumb navigation** (Project → Board → Issue) | ⚠️ Partial | Low — exists but basic |
| **Multi-select issues** (bulk operations) | 🟡 Medium | Medium |
| **Issue link popover** (hover on issue key to preview) | 🟢 Low | Medium |
| **Dashboard page** (configurable gadgets/widgets) | 🔴 High | High |
| **Reports page** (Burndown, Velocity, CFD charts) | 🔴 High | High |
| **Project creation wizard** | 🟡 Medium | Medium |
| **Board column configuration** (reorder, rename, add/remove) | ⚠️ Partial | Low — rename/WIP exists |
| **Avatar image support** (file upload for avatar) | 🟢 Low | Low |

---

## 4. Bugs & Technical Issues Found

### 4.1 Critical Issues

| # | Category | Issue | Location | Risk |
|---|----------|-------|----------|------|
| 1 | **Concurrency** | Issue key generation has race condition — two concurrent creates could generate the same key | `IssueService.cs:218-230` | 🔴 High |
| 2 | **Memory Leak** | `IssueCardControl` creates `Font` objects (`new Font("Segoe UI", 8f, FontStyle.Bold)`) inside `OnPaint` without disposing | `IssueCardControl.cs:115` | 🟡 Medium |
| 3 | **Memory Leak** | `LogoControl.OnPaint` creates `new Font("Segoe UI", 22f)` inside paint handler without disposing | `LoginForm.cs:352` | 🟡 Medium |
| 4 | **Non-null reference risk** | `_session.CurrentUserContext.CurrentUser?.Id ?? 1` falls back to user ID `1` which may not exist | `BoardForm.cs:446`, `IssueDetailsForm.cs:554`, etc. | 🟡 Medium |
| 5 | **Security** | Pre-filled credentials in LoginForm (`admin` / `admin123`) | `LoginForm.cs:47,53` | 🟡 Medium |
| 6 | **Security** | `EnableSensitiveDataLogging()` enabled in production code | `Program.cs:27` | 🟡 Medium |

### 4.2 Code Quality Issues

| # | Category | Issue | Location |
|---|----------|-------|----------|
| 1 | **Swallowed Exceptions** | `SafeUpdateSplitLayout` catches `ArgumentOutOfRangeException` and `InvalidOperationException` with empty handlers | `IssueDetailsForm.cs:204-208` |
| 2 | **Comment editing** | Uses `Microsoft.VisualBasic.Interaction.InputBox` — not a professional UX for comment editing | `IssueDetailsForm.cs:564` |
| 3 | **No input validation** on Comment body length | `CommentService.cs` |
| 4 | **No validation** on Project name/key length or format | `ProjectCommandService.cs` |
| 5 | **No logging framework** — entire codebase has no structured logging (Serilog, NLog, etc.) | Global |
| 6 | **No configuration file** — `appsettings.json` not used, relies on env vars with hardcoded fallbacks | `Program.cs:16-21` |
| 7 | **Manual DI** via `AppSession` — should use `IServiceProvider` | `AppSession.cs` |
| 8 | **GraphicsPath duplication** — `CreateRoundedPath` utility method is duplicated across 4+ files | Multiple files |

### 4.3 Threading & Async Analysis

| Check | Result |
|-------|--------|
| `async void` methods | ✅ None found |
| `.Result` / `.Wait()` blocking calls | ✅ None found |
| UI thread violations | ✅ `BeginInvoke` used correctly for cross-thread calls |
| Deadlock risk | ✅ `SemaphoreSlim` with `WaitAsync` used correctly |
| Event handler leaks | ⚠️ Lambda event handlers in `WireDragging`, `HookClicks` may prevent GC |

### 4.4 SQL Injection Risk

| Check | Result |
|-------|--------|
| Raw SQL queries | ✅ **No raw SQL found** — all data access via EF Core LINQ |
| Parameterized queries | ✅ EF Core handles parameterization |
| SQL injection | ✅ **No risk** |

---

## 5. QA Test Checklist

### 5.1 Authentication Module

| # | Test Case | Type | Priority |
|---|-----------|------|----------|
| 1 | Login with valid credentials → success, navigate to MainForm | Functional | P0 |
| 2 | Login with wrong password → error message displayed | Functional | P0 |
| 3 | Login with non-existent username → error message | Functional | P0 |
| 4 | Login with deactivated user → error message | Functional | P0 |
| 5 | Login with empty username/password → validation error | Functional | P0 |
| 6 | Logout → session cleared, return to LoginForm | Functional | P0 |
| 7 | Login form drag window → form moves correctly | UI | P2 |
| 8 | Show/Hide password toggle works | UI | P1 |

### 5.2 Board Module

| # | Test Case | Type | Priority |
|---|-----------|------|----------|
| 1 | Board displays 4 columns with correct issue counts | Functional | P0 |
| 2 | Active sprint name and date range displayed | Functional | P0 |
| 3 | "No active sprint" message when no sprint is active | Functional | P1 |
| 4 | Start Sprint button starts the next planned sprint | Functional | P0 |
| 5 | Filter by assignee → shows only matching issues | Functional | P0 |
| 6 | Filter by priority → shows only matching issues | Functional | P0 |
| 7 | Filter by type → shows only matching issues | Functional | P0 |
| 8 | Search filter → matches on title and issue key | Functional | P0 |
| 9 | Clear filters → resets all filters | Functional | P1 |
| 10 | Right-click card → "Move to" context menu works | Functional | P0 |
| 11 | Click card → opens IssueDetailsForm | Functional | P0 |
| 12 | "+ Create issue" link → opens IssueEditorForm with default status | Functional | P1 |
| 13 | Board with 0 issues → empty state shown per column | UI | P1 |
| 14 | Board with 50+ issues → no visible lag, scrolling smooth | Performance | P1 |

### 5.3 Issue CRUD

| # | Test Case | Type | Priority |
|---|-----------|------|----------|
| 1 | Create issue with title only → success | Functional | P0 |
| 2 | Create issue without title → validation error | Functional | P0 |
| 3 | Create issue with negative story points → validation error | Functional | P1 |
| 4 | Edit issue title (inline click → edit → save) | Functional | P0 |
| 5 | Edit issue title (Escape cancels edit) | Functional | P1 |
| 6 | Change status via dropdown → saved immediately | Functional | P0 |
| 7 | Change priority via dropdown → saved immediately | Functional | P0 |
| 8 | Assign/unassign users via picker dialog | Functional | P0 |
| 9 | Add comment → appears in list | Functional | P0 |
| 10 | Edit comment → updated body shown | Functional | P0 |
| 11 | Delete comment → soft-deleted, removed from list | Functional | P0 |
| 12 | Upload attachment → file stored with checksum | Functional | P0 |
| 13 | Download attachment → file saved to chosen location | Functional | P0 |
| 14 | Delete attachment → soft-deleted | Functional | P1 |
| 15 | Delete issue → confirmation dialog → soft-deleted | Functional | P0 |
| 16 | Log time → hours added to time tracking bar | Functional | P1 |
| 17 | Activity log shows all changes chronologically | Functional | P1 |

### 5.4 Sprint Module

| # | Test Case | Type | Priority |
|---|-----------|------|----------|
| 1 | Create sprint with name, goal, dates | Functional | P0 |
| 2 | Start sprint → state changes to Active | Functional | P0 |
| 3 | Start sprint when another active → error message | Functional | P0 |
| 4 | Close sprint → move incomplete to next sprint | Functional | P0 |
| 5 | Close sprint → move incomplete to backlog | Functional | P0 |
| 6 | Assign issues to sprint via dialog | Functional | P0 |

### 5.5 User & Permission Module

| # | Test Case | Type | Priority |
|---|-----------|------|----------|
| 1 | Admin creates user → success | Functional | P0 |
| 2 | Admin deactivates user → user cannot login | Functional | P0 |
| 3 | Viewer tries to create issue → unauthorized error | Functional | P0 |
| 4 | Viewer tries to start sprint → unauthorized error | Functional | P0 |
| 5 | Developer can create/edit issues but not sprints | Functional | P0 |
| 6 | Admin can access all functionality | Functional | P0 |
| 7 | Reset password → user can login with new password | Functional | P1 |

### 5.6 Edge Cases & Regression

| # | Test Case | Priority |
|---|-----------|----------|
| 1 | Create issue when database is offline → graceful error | P0 |
| 2 | Concurrent issue creation → unique issue keys | P0 |
| 3 | Empty project (0 issues, 0 sprints) → no crashes | P0 |
| 4 | Issue title max length (edge: 1 char, 500+ chars) | P1 |
| 5 | Comment body with special characters / very long text | P1 |
| 6 | Upload large attachment (>100MB) → error or success | P1 |
| 7 | Resize window to minimum size → no layout crash | P1 |
| 8 | Rapid-click "Save" button → no duplicate operations | P1 |
| 9 | Login → Logout → Login → forms reset properly | P1 |

---

## 6. Technical Debt Summary

| # | Debt Item | Impact | Effort to Fix |
|---|----------|--------|---------------|
| 1 | Manual DI via `AppSession` → should use `IServiceCollection`/DI container | Maintainability | Medium |
| 2 | Duplicated `CreateRoundedPath()` utility across 4+ controls | Code duplication | Low |
| 3 | No structured logging (Serilog/NLog) | Debuggability | Low |
| 4 | No `appsettings.json` / configuration system | Operations | Low |
| 5 | Hardcoded credentials in LoginForm | Security | Low (remove defaults) |
| 6 | `EnableSensitiveDataLogging()` in production | Security | Low (env-conditional) |
| 7 | `Microsoft.VisualBasic.Interaction.InputBox` for comment editing | UX | Medium (custom dialog) |
| 8 | Font objects created in paint handlers without disposal | Memory | Low |
| 9 | No cancellation token propagation in UI layer | Reliability | Medium |
| 10 | Event handler lambdas not unsubscribed (potential GC prevention) | Memory | Medium |

---

## 7. Recommended Implementation Order

### Phase 1 — Foundation Fixes (Priority: Critical, ~2-3 weeks)

1. **Fix issue key race condition** — use DB sequence or `MAX()` in transaction
2. **Fix font disposal** in paint handlers (`IssueCardControl`, `LoginForm`)
3. **Remove hardcoded credentials** from LoginForm
4. **Add `appsettings.json`** with environment-specific config
5. **Disable `EnableSensitiveDataLogging`** in production
6. **Replace `VB.InputBox`** with custom WinForms dialog for comment editing

### Phase 2 — Core Missing Features (Priority: High, ~4-6 weeks)

7. **Add Epic & Subtask issue types** — new `IssueType` enum values + parent-child relationship
8. **Implement drag & drop** on board (WinForms `DoDragDrop` / `DragEnter` / `DragDrop`)
9. **Add Dashboard page** — project summary widgets (issue counts, sprint progress, recent activity)
10. **Add basic Reports** — Burndown chart (story points over sprint days), Velocity chart

### Phase 3 — Enhanced Features (Priority: Medium, ~4-6 weeks)

11. **Rich text editor** for description (integrate WebBrowser control with Markdown preview, or use a RichTextBox with formatting toolbar)
12. **Labels & Components** — new entities, UI for assignment
13. **Versions / Releases** — Fix Version entity, release management UI
14. **Multi-project support** — project switcher in sidebar
15. **Project creation wizard** — UI for creating new projects

### Phase 4 — Advanced Features (Priority: Low, ~6-8 weeks)

16. **Custom workflow engine** — configurable statuses and transition rules
17. **JQL-like search** — simple query parser for issue filtering
18. **Saved Filters** — persist and recall search criteria
19. **Notification system** — in-app notification panel
20. **Keyboard shortcuts** — global hotkeys for common actions

---

## 8. Estimated Effort Overview

| Phase | Scope | Estimated Effort |
|-------|-------|-----------------|
| **Phase 1** | Foundation Fixes | 2-3 weeks |
| **Phase 2** | Core Missing Features | 4-6 weeks |
| **Phase 3** | Enhanced Features | 4-6 weeks |
| **Phase 4** | Advanced Features | 6-8 weeks |
| **Total** | Full Jira-like feature set | **16-23 weeks** |

> **Note:** Estimates assume a single full-time developer. Parallel work could reduce timeline significantly.

---

## Appendix A: File Inventory

### Source Files by Layer

| Layer | Files | Description |
|-------|-------|-------------|
| `JiraClone.Domain` | 16 | Entities (10), Enums (5), Common (2) |
| `JiraClone.Application` | 17 | Services (7), Abstractions (12), Models (5) |
| `JiraClone.Infrastructure` | 3 | Security (2), Storage (1) |
| `JiraClone.Persistence` | 34 | DbContext (3), Configs (12), Repos (8), Migrations (7), Seed (1), Schema (3) |
| `JiraClone.WinForms` | 25 | Forms (7), Controls (7), Theme (5), Composition (1), Services (1), ViewModels (1), Program (1) |
| **Tests** | 17 | Application (6), Domain (1), Integration (3), Persistence (7) |
| **Total** | **112** | — |
