# Phase 1 — Critical Fixes Summary

> **Date:** 2026-03-17 | **Build:** ✅ 0 errors | **Tests:** ✅ 53/53 passed

---

## BUG 1 — Race Condition: Issue Key Generation  
**Severity:** 🔴 Critical | **Category:** Concurrency

**Root Cause:** `GenerateIssueKeyAsync` fetched all project issues into memory, computed `MAX(sequence)+1` — two concurrent creates would read the same MAX and generate duplicate keys.

**Fix:** Added `GetNextIssueSequenceAsync` using raw SQL with `UPDLOCK, HOLDLOCK` hints to atomically read the max sequence under a row-level lock.

### Files Changed
| File | Change |
|------|--------|
| [IIssueRepository.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.Application/Abstractions/IIssueRepository.cs) | Added `GetNextIssueSequenceAsync` to interface |
| [IssueRepository.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.Persistence/Repositories/IssueRepository.cs) | Implemented atomic SQL with `UPDLOCK, HOLDLOCK` |
| [IssueService.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.Application/Issues/IssueService.cs) | Simplified `GenerateIssueKeyAsync`, removed `TryParseIssueSequence` |
| [IssueServiceTests.cs](file:///d:/Ki2Nam4/.Net/jira_clone/tests/JiraClone.Tests/Application/IssueServiceTests.cs) | Updated 2 test mocks + helper to use new method |

```diff
-var projectIssues = await _issues.GetProjectIssuesAsync(project.Id, cancellationToken);
-var maxSequence = projectIssues.Select(...).Max();
-return $"{prefix}{maxSequence + 1}";
+var nextSequence = await _issues.GetNextIssueSequenceAsync(project.Id, cancellationToken);
+return $"{prefix}{nextSequence}";
```

---

## BUG 2 — Memory Leak: Font in IssueCardControl.OnPaint  
**Severity:** 🟡 Medium | **Category:** Memory

**Root Cause:** `new Font("Segoe UI", 8f, FontStyle.Bold)` created on every `OnPaint` call (60+ times/sec during scrolling) without `Dispose()`.

**Fix:** Cached as `private static readonly Font AvatarFont`.

### Files Changed
| File | Change |
|------|--------|
| [IssueCardControl.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.WinForms/Controls/IssueCardControl.cs) | Added static `AvatarFont` field, replaced inline allocation |

```diff
+private static readonly Font AvatarFont = new("Segoe UI", 8f, FontStyle.Bold);
 ...
-new Font("Segoe UI", 8f, FontStyle.Bold),
+AvatarFont,
```

---

## BUG 3 — Memory Leak: LogoControl Font  
**Severity:** 🟡 Medium | **Category:** Memory

**Root Cause:** Same issue as BUG 2 — `new Font("Segoe UI", 22f, FontStyle.Bold)` in `LogoControl.OnPaint`.

**Fix:** Cached as `private static readonly Font LogoFont`.

### Files Changed
| File | Change |
|------|--------|
| [LoginForm.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.WinForms/Forms/LoginForm.cs) | Added static `LogoFont` field in `LogoControl` inner class |

---

## BUG 4 — Null Fallback `?? 1`  
**Severity:** 🟡 Medium | **Category:** Runtime Safety

**Root Cause:** `_session.CurrentUserContext.CurrentUser?.Id ?? 1` silently fell back to user ID `1`, which may not exist or could attribute actions to the wrong user.

**Fix:** Added `RequireUserId()` method to `CurrentUserContext` that throws `InvalidOperationException("No user is currently logged in.")`. Replaced all 9 occurrences across 3 files.

### Files Changed
| File | Change |
|------|--------|
| [CurrentUserContext.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.Infrastructure/Security/CurrentUserContext.cs) | Added `RequireUserId()` method |
| [BoardForm.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.WinForms/Forms/BoardForm.cs) | 1 replacement |
| [IssueEditorForm.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.WinForms/Forms/IssueEditorForm.cs) | 1 replacement |
| [IssueDetailsForm.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.WinForms/Forms/IssueDetailsForm.cs) | 7 replacements |

```diff
-_session.CurrentUserContext.CurrentUser?.Id ?? 1
+_session.CurrentUserContext.RequireUserId()
```

---

## BUG 5 — Swallowed Exception  
**Severity:** 🟢 Low | **Category:** Diagnostics

**Root Cause:** `SafeUpdateSplitLayout` caught `ArgumentOutOfRangeException` and `InvalidOperationException` with empty handlers — made debugging layout issues impossible.

**Fix:** Added `Debug.WriteLine` to log exception messages for diagnostics.

### Files Changed
| File | Change |
|------|--------|
| [IssueDetailsForm.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.WinForms/Forms/IssueDetailsForm.cs) | Added debug logging to catch handlers |

```diff
-catch (ArgumentOutOfRangeException)
-{
-}
+catch (ArgumentOutOfRangeException ex)
+{
+    System.Diagnostics.Debug.WriteLine($"SplitLayout range error: {ex.Message}");
+}
```

---

## BUG 6 — Hardcoded Credentials  
**Severity:** 🟡 Medium | **Category:** Security

**Root Cause:** LoginForm pre-filled `admin` / `admin123` in text boxes — security risk in production.

**Fix:** Set both text boxes to `string.Empty`.

### Files Changed
| File | Change |
|------|--------|
| [LoginForm.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.WinForms/Forms/LoginForm.cs) | Cleared pre-filled username and password |

---

## BUG 7 — SensitiveDataLogging in Production  
**Severity:** 🟡 Medium | **Category:** Security

**Root Cause:** `EnableSensitiveDataLogging()` was always enabled, logging sensitive DB parameters in production.

**Fix:** Wrapped in `#if DEBUG` preprocessor directive.

### Files Changed
| File | Change |
|------|--------|
| [Program.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.WinForms/Program.cs) | Wrapped `EnableSensitiveDataLogging()` in `#if DEBUG` |

```diff
 services.AddDbContextFactory<JiraCloneDbContext>(options =>
-    options.UseSqlServer(connectionString).EnableSensitiveDataLogging());
+{
+    options.UseSqlServer(connectionString);
+#if DEBUG
+    options.EnableSensitiveDataLogging();
+#endif
+});
```

---

## BUG 8 — Hardcoded Connection String  
**Severity:** 🟡 Medium | **Category:** Configuration

**Root Cause:** Connection string and attachment path were hardcoded with env var fallback only — no way to change without code changes or env vars.

**Fix:** Added `appsettings.json` with `Microsoft.Extensions.Configuration`. Config priority: `appsettings.json` → environment variables → hardcoded default.

### Files Changed
| File | Change |
|------|--------|
| [Program.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.WinForms/Program.cs) | Added `ConfigurationBuilder` with JSON + env vars |
| [appsettings.json](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.WinForms/appsettings.json) | **[NEW]** Configuration file |
| [JiraClone.WinForms.csproj](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.WinForms/JiraClone.WinForms.csproj) | Added NuGet packages + CopyToOutput |

### New NuGet Packages
- `Microsoft.Extensions.Configuration.Json` 8.0.1
- `Microsoft.Extensions.Configuration.EnvironmentVariables` 8.0.0

---

## BUG 9 — VB.InputBox for Comment Editing  
**Severity:** 🟢 Low | **Category:** UX

**Root Cause:** `Microsoft.VisualBasic.Interaction.InputBox` provides a tiny single-line dialog — unprofessional for editing multi-line comments.

**Fix:** Created `CommentEditDialog` with multiline TextBox, OK/Cancel, Jira theme styling.

### Files Changed
| File | Change |
|------|--------|
| [CommentEditDialog.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.WinForms/Dialogs/CommentEditDialog.cs) | **[NEW]** Proper WinForms dialog |
| [IssueDetailsForm.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.WinForms/Forms/IssueDetailsForm.cs) | Replaced `InputBox` with `CommentEditDialog` |

---

## BUG 10 — Duplicated Utility Method  
**Severity:** 🟢 Low | **Category:** Code Quality

**Root Cause:** `CreateRoundedPath(Rectangle, int)` was duplicated in 3 files (IssueCardControl, BoardColumnControl, LoginForm).

**Fix:** Extracted to `GraphicsHelper.CreateRoundedPath` static method. Removed all 3 duplicates, replaced with `GraphicsHelper.CreateRoundedPath(...)`.

### Files Changed
| File | Change |
|------|--------|
| [GraphicsHelper.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.WinForms/Helpers/GraphicsHelper.cs) | **[NEW]** Shared utility |
| [IssueCardControl.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.WinForms/Controls/IssueCardControl.cs) | Removed local method, added using |
| [BoardColumnControl.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.WinForms/Controls/BoardColumnControl.cs) | Removed local method, added using |
| [LoginForm.cs](file:///d:/Ki2Nam4/.Net/jira_clone/src/JiraClone.WinForms/Forms/LoginForm.cs) | Removed local method, added using |

---

## Verification Results

| Check | Result |
|-------|--------|
| **Build** | ✅ 0 errors, 0 warnings |
| **Tests** | ✅ 53/53 passed |
| **New files** | 3 (`CommentEditDialog.cs`, `GraphicsHelper.cs`, `appsettings.json`) |
| **Modified files** | 13 |
| **New NuGet packages** | 2 |
