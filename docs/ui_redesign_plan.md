# JiraClone WinForms — Jira Web-Style UI Redesign Plan

## Goal

Redesign all WinForms forms/controls to closely match the **Atlassian Jira web application** look-and-feel, using the **Atlassian Design System 2024** color palette, typography, layout patterns, and component styling. Transform the current default WinForms appearance into a polished, professional desktop clone of Jira.

---

## Atlassian Design System Specs

### Color Palette (Light Mode)

| Token | Hex | Usage |
|---|---|---|
| **Blue500** | `#4688EC` | Primary buttons, links, selected items |
| **Blue600** | `#357DE8` | Primary button hover |
| **Blue700** | `#1868DB` | Active/pressed states |
| **Blue100** | `#E9F2FE` | Selected row background, info badges |
| **Neutral0** | `#FFFFFF` | Card & dialog backgrounds |
| **Neutral100** | `#F8F8F8` | Page/section background |
| **Neutral200** | `#F0F1F2` | Board column background, alternate rows |
| **Neutral300** | `#DDDEE1` | Borders, dividers, card outlines |
| **Neutral500** | `#8C8F97` | Secondary text, placeholders |
| **Neutral700** | `#6B6E76` | Subtle text |
| **Neutral1100** | `#1E1F21` | Primary text |
| **Red500** | `#F15B50` | Bug icon, danger buttons, highest priority |
| **Red700** | `#C9372C` | Danger hover |
| **Green500** | `#2ABB7F` | Success, Done status |
| **Green700** | `#1F845A` | Success badge background |
| **Orange400** | `#FCA700` | Warning, Medium priority |
| **Yellow300** | `#EED12B` | Low priority |
| **Teal500** | `#42B2D7` | Info badges, Story icon |
| **Purple500** | `#BF63F3` | Epic icon |

### Semantic Colors (Jira-specific)

| Element | Color | Hex |
|---|---|---|
| Sidebar background | Dark Blue | `#1C2B42` (Blue1000) |
| Sidebar active item | Blue highlight | `#357DE8` (Blue600) |
| Sidebar text | White | `#FFFFFF` |
| Sidebar hover | Semi-transparent white | `rgba(255,255,255,0.08)` |
| Top bar background | White | `#FFFFFF` |
| Board column header text | Neutral700 | `#6B6E76` |
| Board column background | Neutral200 | `#F0F1F2` |
| Card background | White | `#FFFFFF` |
| Card border | Neutral300 | `#DDDEE1` |
| Card shadow | Subtle | `0 1px 2px rgba(0,0,0,0.08)` |
| Issue key text | Blue600 | `#357DE8` |
| Done column badge | Green | `#DCFFF1` bg, `#1F845A` text |
| In Progress badge | Blue | `#E9F2FE` bg, `#357DE8` text |

### Issue Type Icons (Color Coding)

| Type | Shape | Color |
|---|---|---|
| Story | Bookmark/shield | Green `#4BCE97` |
| Task | Checkbox | Blue `#669DF1` |
| Bug | Circle/dot | Red `#F87168` |
| Epic | Lightning | Purple `#C97CF4` |

### Priority Icons (Arrow Colors)

| Priority | Icon | Color |
|---|---|---|
| Highest | ⬆⬆ double up | Red `#C9372C` |
| High | ⬆ up | Red `#F87168` |
| Medium | ⬌ equal | Orange `#FCA700` |
| Low | ⬇ down | Green `#4BCE97` |
| Lowest | ⬇⬇ double down | Green `#2ABB7F` |

### Typography

| Element | Font | Size | Weight | Color |
|---|---|---|---|---|
| App font | **Segoe UI** (Windows) / Inter fallback | — | — | — |
| Page heading | Segoe UI | 20px | SemiBold (600) | Neutral1100 `#1E1F21` |
| Section heading | Segoe UI | 14px | SemiBold (600) | Neutral1100 |
| Body text | Segoe UI | 13px | Regular (400) | Neutral1100 |
| Secondary text | Segoe UI | 12px | Regular | Neutral700 `#6B6E76` |
| Column header | Segoe UI | 12px | Bold (700) uppercase | Neutral700 |
| Card title | Segoe UI | 13px | Regular | Neutral1100 |
| Card issue key | Segoe UI | 12px | Regular | Blue600 `#357DE8` |
| Button text | Segoe UI | 13px | Medium (500) | White (primary) / Neutral1100 (secondary) |

### Spacing (8pt Grid)

| Token | Value |
|---|---|
| xs | 4px |
| sm | 8px |
| md | 12px |
| lg | 16px |
| xl | 24px |
| 2xl | 32px |

---

## Current State vs Target

**Current**: All forms use default WinForms `SystemColors`, no custom backgrounds/fonts/borders. ListView/Button/Panel/TabControl all have Windows default styling. No color-coded icons, no sidebar, no card-like UI.

**Target**: Every form redrawn with Atlassian colors, custom-rendered cards, sidebar navigation, modern flat buttons, status badges, priority/type icons.

---

## Proposed Changes

### 1. Theme Infrastructure (NEW files)

#### [NEW] `JiraTheme.cs` — `JiraClone.WinForms/Theme/JiraTheme.cs`

Central static class holding all Atlassian Design System color constants, font factories, spacing values, and helper methods. All forms reference this instead of hardcoded values.

```
Contents:
- static Color fields for all palette tokens
- Font factory methods (Heading, Body, Secondary, ColumnHeader, CardTitle, IssueKey)
- Spacing constants (Xs=4, Sm=8, Md=12, Lg=16, Xl=24)
- Helper: CreateFlatButton(text, isPrimary)
- Helper: CreateCard(panel) → applies white bg, border, radius, shadow
- Helper: StyleListView(listView) → Jira-like styling
- Helper: ApplyFormTheme(form) → base form styling
```

#### [NEW] `JiraIcons.cs` — `JiraClone.WinForms/Theme/JiraIcons.cs`

Static class that generates GDI+ icons for issue types and priorities at runtime (no external image files needed). Uses colored shapes matching Jira's visual language.

```
Contents:
- Bitmap GetIssueTypeIcon(IssueType, size=16)
- Bitmap GetPriorityIcon(IssuePriority, size=16)
- ImageList CreateIssueTypeImageList()
- ImageList CreatePriorityImageList()
```

---

### 2. MainForm Redesign

#### [MODIFY] `MainForm.cs` — `JiraClone.WinForms/Forms/MainForm.cs`

**Current**: Basic `TabControl` with 4 tabs (Board/Sprints/Users/Settings).  
**Target**: Replace TabControl with a **collapsible left sidebar** (Panel) matching Jira's dark navy sidebar + main content area.

Changes:
- Add left `Panel` (width=240px, BackColor=`#1C2B42`) as sidebar
- Add navigation buttons in sidebar styled as Jira nav items (icon + label, highlight active)
- Navigation items: Board, Backlog (future), Sprints, Users, Project Settings
- Project icon + name at top of sidebar
- Current user display at bottom of sidebar
- Main content area uses `Panel` that swaps child UserControls on nav click
- Remove `TabControl` entirely

---

### 3. LoginForm Redesign

#### [MODIFY] `LoginForm.cs` — `JiraClone.WinForms/Forms/LoginForm.cs`

**Current**: 68L basic form with labels/textboxes/button.  
**Target**: Centered card-style login matching Atlassian account login page.

Changes:
- Form background: Neutral200 `#F0F1F2`
- Centered white card panel (400×380px, rounded corners effect)
- Atlassian blue logo/branding at top (Jira icon)
- "Log in to continue" heading text
- Styled TextBoxes with Neutral300 border, rounded corners (custom paint)
- Primary blue Login button (`#4688EC`, white text, hover `#357DE8`)
- Error message label in Red (`#C9372C`)
- Font: Segoe UI throughout

---

### 4. BoardForm Redesign (Most Critical)

#### [MODIFY] `BoardForm.cs` — `JiraClone.WinForms/Forms/BoardForm.cs`

**Current**: 210L with FlowLayoutPanel columns, basic filter dropdowns.  
**Target**: Pixel-perfect Jira Kanban board.

Changes:
- **Toolbar area** (top): White background, project name as heading, search TextBox with icon, filter ComboBoxes styled flat, avatar circles for quick filter
- **Column headers**: Uppercase text in Neutral700, small font, item count badge
- **Columns**: Neutral200 background `#F0F1F2`, 8px gap between columns, scroll overflow
- **Cards redesign** (via `IssueCardControl`): White background, 1px Neutral300 border, subtle shadow on hover, rounded corners (4px). Content layout:
  - Issue title (13px, Neutral1100)
  - Bottom row: Issue type icon (colored) + Issue key (Blue600 link-style) + priority icon + assignee avatar circle (right-aligned)
- **Drag hint**: Mouse cursor changes on card hover (grab cursor effect via custom paint)

#### [MODIFY] `IssueCardControl.cs` — `JiraClone.WinForms/Controls/IssueCardControl.cs`

**Current**: 40L basic Panel with label.  
**Target**: Custom owner-drawn card matching Jira card design.

Changes:
- Custom `OnPaint` with card rendering
- White bg, Neutral300 border, rounded corners
- Title text in Neutral1100
- Bottom row with: type icon (colored shape), key text (Blue600), priority icon, assignee avatar circle
- Hover effect: slightly darker border or subtle shadow
- Cursor: Hand on hover

#### [MODIFY] `BoardColumnControl.cs` — `JiraClone.WinForms/Controls/BoardColumnControl.cs`

**Current**: 54L basic Panel.  
**Target**: Jira-style board column.

Changes:
- Background: Neutral200 `#F0F1F2`
- Header: uppercase label (Neutral700), item count in parentheses
- Rounded top corners (4px)
- Minimum height fills available space
- Scrollable when cards overflow
- 8px padding, 8px gap between cards

---

### 5. IssueEditorForm Redesign

#### [MODIFY] `IssueEditorForm.cs` — `JiraClone.WinForms/Forms/IssueEditorForm.cs`

**Current**: 157L TableLayoutPanel-based form.  
**Target**: Jira-style create/edit issue dialog.

Changes:
- Dialog background: White
- Section labels in SemiBold Neutral700
- TextBoxes/ComboBoxes with Neutral300 border styling
- Primary button (Save) in Blue500, Secondary (Cancel) in Neutral200
- Issue type selector with colored icons
- Priority selector with colored arrow icons
- Assignee multi-select with avatar chips

---

### 6. IssueDetailsForm Redesign

#### [MODIFY] `IssueDetailsForm.cs` — `JiraClone.WinForms/Forms/IssueDetailsForm.cs`

**Current**: 222L dialog with TabControl for Comments/Attachments/Activity.  
**Target**: Jira-style issue detail view (2-column layout).

Changes:
- **Left panel** (65%): Issue title (large, editable), description, Comments section, Activity section
- **Right panel** (35%): Status badge (colored), Priority, Type, Assignee(s), Sprint, Story Points, Time Tracking
- Status badge: colored background (Blue100 for In Progress, Green100 for Done, Neutral200 for Backlog)
- Action buttons in toolbar: Edit, Delete styled flat

---

### 7. SprintManagementForm Redesign

#### [MODIFY] `SprintManagementForm.cs` — `JiraClone.WinForms/Forms/SprintManagementForm.cs`

**Current**: 241L ListView + buttons.  
**Target**: Jira-style sprint panel (collapsible sections per sprint).

Changes:
- Sprint sections with header bar (sprint name, dates, status badge)
- Status badges: Green for Active, Neutral for Planned, Gray for Closed
- Action buttons styled as Jira buttons (flat, primary color)
- Issue list under each sprint as mini-cards

---

### 8. UserManagementForm & ProjectSettingsForm

#### [MODIFY] `UserManagementForm.cs` — `JiraClone.WinForms/Forms/UserManagementForm.cs`

Changes:
- ListView with alternating row colors (White / Neutral100)
- Flat-styled action buttons (Blue500 primary, Neutral secondary)
- Status column with green/red colored badges
- User avatar circles in list

#### [MODIFY] `ProjectSettingsForm.cs` — `JiraClone.WinForms/Forms/ProjectSettingsForm.cs`

Changes:
- TabControl restyled (custom-drawn tabs matching Jira's underline tab style)
- Form fields with Neutral300 borders
- Flat action buttons
- Member list with avatar + role badge

---

### 9. Common Controls Restyling

#### [MODIFY] All Controls in `Controls/`

- `CommentListControl`: Chat-bubble style comments with avatar, timestamp in Secondary text
- `AttachmentListControl`: File cards with icon, filename, size, download link-style button  
- `AttachmentPicker`: Drop zone style with dashed Neutral300 border
- `ActivityTimelineControl`: Timeline with icons per action type, timestamps in Secondary text

---

## Key Decisions

1. **Sidebar vs TabControl**: Plan replaces TabControl with a proper sidebar. Confirm this is desired.
2. **Color-coded icons**: Generated at runtime with GDI+. Alternative: embed PNG resources.
3. **Custom-painted controls vs. Third-party library**: We can use a library like `MaterialSkin` or `MetroFramework` instead of hand-painting.
4. **Scope**: Implement all at once, or phase it (Theme → BoardForm → other forms)?

---

## Verification Plan

### Manual Verification (User Testing)

1. **Build and launch** the app: `dotnet build` then run `JiraClone.WinForms.exe`
2. **Login screen**: Verify centered card layout, blue button, proper fonts, error message styling
3. **Main layout**: Verify dark sidebar with nav items, active highlight, correct colors
4. **Board view**: Verify column backgrounds (F0F1F2), card styling (white bg, border, shadow), colored type/priority icons, issue key in blue
5. **Issue detail**: Verify 2-column layout, status badge colors, right sidebar fields
6. **Sprint management**: Verify sprint section headers, status badges
7. **User management**: Verify alternating rows, flat buttons, status badges
8. **Project settings**: Verify tab styling, flat buttons, member list with roles
