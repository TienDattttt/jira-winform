# E2E TEST DESIGN INPUT

## MỤC 1 — FORM INVENTORY & CONTROL TYPE AUDIT

### Ghi chú scan
- Phạm vi scan: `src/JiraClone.WinForms/Forms/**/*.cs`
- Repo scan `AutomationId`, `AccessibleName`, `AccessibleDescription` trong `Forms` trả về: **KHÔNG TÌM THẤY**
- Vì vậy mọi control bên dưới đều ghi `AutomationId: KHÔNG TÌM THẤY` trừ khi có ghi chú khác

### 1a + 1c. Form inventory, rendering type, navigation trigger

| Form/Class | File | Loại rendering | Mở bằng cách nào | Đóng bằng cách nào | Navigate tiếp theo |
|---|---|---|---|---|---|
| `LoginForm` | `src/JiraClone.WinForms/Forms/LoginForm.cs` | Hỗn hợp: standard controls + custom `ShadowPanel`, `LogoControl` (`OnPaint`) | `Program.CreateStartupForm()` trả về `new LoginForm(...)` khi không restore được session | Nút `_closeButton`, phím `Esc`, hoặc `Close()` sau login thành công | Sau login thành công mở `MainForm` bằng `ShowDialog(this)` |
| `MainForm` | `src/JiraClone.WinForms/Forms/MainForm.cs` | Hỗn hợp: standard controls + custom `SidebarNavItem`, `InitialsAvatar`, `NotificationRowControl`, panel vẽ tay | `Program.CreateStartupForm()` trả về `new MainForm(...)` khi restore session thành công hoặc sau login | `Close()` khi logout/restart hoặc người dùng đóng cửa sổ | Điều hướng sang các `UserControl` con qua `NavigateTo(...)`; cũng mở `IssueEditorForm` và `IssueDetailsForm` từ action/notification |
| `ProjectListForm` | `src/JiraClone.WinForms/Forms/ProjectListForm.cs` | Hỗn hợp: standard controls + custom `ProjectCard` (`OnPaint`) + `surface.Paint` | `MainForm.OnProjectsItemClick()` / `CreateProjectListControl()` | Không tự đóng; bị dispose khi `MainForm.NavigateTo(...)` form khác | Có thể mở `CreateProjectForm`; sau khi chọn project thì raise `ProjectOpened` để `MainForm` chuyển sang `BoardForm` |
| `DashboardForm` | `src/JiraClone.WinForms/Forms/DashboardForm.cs` | Hỗn hợp nặng: standard controls + nhiều panel custom override `OnPaint` | `MainForm.OnDashboardItemClick()` | Không tự đóng; bị dispose khi navigate form khác | Từ row/cell action mở `IssueDetailsForm` |
| `BoardForm` | `src/JiraClone.WinForms/Forms/BoardForm.cs` | Hỗn hợp: standard filter/button + custom `BoardColumnControl`, `EpicSwimlaneControl`, `IssueCardControl`, panel toast tự vẽ | `MainForm.OnBoardItemClick()` cho Board Scrum; `MainForm.OnBacklogItemClick()` cho Backlog (`activeSprintOnly: false`) | Không tự đóng; bị dispose khi navigate form khác | Mở `IssueEditorForm` để tạo issue; mở `IssueDetailsForm` khi mở card/epic |
| `RoadmapForm` | `src/JiraClone.WinForms/Forms/RoadmapForm.cs` | Hỗn hợp nặng: standard controls + `ListBox` owner-draw + `RoadmapTimelineCanvas` custom draw | `MainForm.OnRoadmapItemClick()` | Không tự đóng; bị dispose khi navigate form khác | Mở `IssueDetailsForm` khi click/double click epic hoặc child issue |
| `SprintManagementForm` | `src/JiraClone.WinForms/Forms/SprintManagementForm.cs` | Hỗn hợp: standard controls + `surface.Paint` | `MainForm.OnSprintsItemClick()` | Không tự đóng; bị dispose khi navigate form khác | Mở `CreateSprintDialog`, `AssignToSprintDialog`, `CloseSprintDialog` |
| `IssueNavigatorForm` | `src/JiraClone.WinForms/Forms/IssueNavigatorForm.cs` | Hỗn hợp: standard controls + custom `JqlEditorControl` + `surface.Paint` | `MainForm.OnIssuesItemClick()` | Không tự đóng; bị dispose khi navigate form khác | Mở `IssueEditorForm`, `IssueDetailsForm`, `SaveFilterDialog` |
| `ReportsForm` | `src/JiraClone.WinForms/Forms/ReportsForm.cs` | Hỗn hợp nặng: standard `TabControl`, `ComboBox`, `Button` + custom chart/card panels override `OnPaint` | `MainForm.OnReportsItemClick()` | Không tự đóng; bị dispose khi navigate form khác | KHÔNG TÌM THẤY mở sang form custom khác; export dùng dialog hệ thống |
| `UserManagementForm` | `src/JiraClone.WinForms/Forms/UserManagementForm.cs` | Hỗn hợp: standard controls + `surface.Paint` | `MainForm.OnUsersItemClick()`; chỉ đi tới đây khi user là `Admin` | Không tự đóng; bị dispose khi navigate form khác | Mở `UserEditorDialog`, `ResetPasswordDialog` |
| `ProjectSettingsForm` | `src/JiraClone.WinForms/Forms/ProjectSettingsForm.cs` | Hỗn hợp: standard controls + custom controls con (`ProfileSettingsControl`, `WorkflowSettingsControl`, `IntegrationSettingsControl`) + nhiều dialog nested | `MainForm.OnSettingsItemClick()` | Không tự đóng; bị dispose khi navigate form khác | Mở `WebhookEndpointDialog`, `WebhookDeliveryHistoryForm`, và nhiều dialog nested trong chính file |
| `CreateProjectForm` | `src/JiraClone.WinForms/Forms/CreateProjectForm.cs` | Hỗn hợp: standard controls + `surface.Paint` quanh member grid | `ProjectListForm.CreateProjectAsync()` | Nút `_cancelButton`; `DialogResult.OK` + `Close()` sau tạo thành công | Caller (`ProjectListForm`) set active project và raise `ProjectOpened` |
| `IssueEditorForm` | `src/JiraClone.WinForms/Forms/IssueEditorForm.cs` | Hỗn hợp: standard controls + custom `SprintSelectorControl` | Từ `BoardForm`, `IssueNavigatorForm`, `MainForm` top action, `IssueDetailsForm` child issue flow | `_cancelButton`; `DialogResult.OK` + `Close()` sau lưu | Caller refresh dữ liệu issue/board sau khi dialog đóng |
| `IssueDetailsForm` | `src/JiraClone.WinForms/Forms/IssueDetailsForm.cs` | Hỗn hợp nặng: standard controls + custom `MarkdownEditorControl`, `MarkdownViewerControl`, `AttachmentPicker`, `AttachmentListControl`, `ActivityTimelineControl`, `IssueIntegrationsControl`, `AvatarValueControl`, `TimeTrackingBar` | Từ `BoardForm`, `DashboardForm`, `IssueNavigatorForm`, `MainForm` notification, `RoadmapForm`, và từ chính `IssueDetailsForm` (parent/child link) | `Close()` khi người dùng đóng, sau delete, hoặc một số error path | Có thể mở tiếp `IssueDetailsForm`, `IssueEditorForm`, `LogTimeDialog`, `LabelPickerDialog`, `ExistingIssuePickerDialog`, `AssigneePickerDialog` |
| `CreateApiTokenDialog` | `src/JiraClone.WinForms/Forms/CreateApiTokenDialog.cs` | Standard WinForms controls | `ProfileSettingsControl` tạo bằng `new CreateApiTokenDialog()` | `DialogResult.OK/Cancel` + `Close()` | Sau OK, caller mở `GeneratedApiTokenDialog` |
| `GeneratedApiTokenDialog` | `src/JiraClone.WinForms/Forms/GeneratedApiTokenDialog.cs` | Standard WinForms controls | `ProfileSettingsControl` mở sau khi tạo token thành công | Nút `Close` (`DialogResult.OK`) hoặc đóng cửa sổ | KHÔNG TÌM THẤY navigate sang form khác |
| `ResetPasswordDialog` | `src/JiraClone.WinForms/Forms/ResetPasswordDialog.cs` | Standard WinForms controls | `UserManagementForm.ResetPasswordAsync()` | `_okButton` hoặc `_cancelButton` | KHÔNG TÌM THẤY navigate sang form khác |
| `WebhookEndpointDialog` | `src/JiraClone.WinForms/Forms/WebhookEndpointDialog.cs` | Standard WinForms controls | `ProjectSettingsForm.AddWebhookAsync()` / `EditWebhookAsync()` | `_okButton` hoặc `_cancelButton` | KHÔNG TÌM THẤY navigate sang form khác |
| `WebhookDeliveryHistoryForm` | `src/JiraClone.WinForms/Forms/WebhookDeliveryHistoryForm.cs` | Standard WinForms controls | `ProjectSettingsForm.ViewWebhookHistoryAsync()` hoặc double click grid webhook | Đóng cửa sổ; error path gọi `Close()` | KHÔNG TÌM THẤY navigate sang form khác |
| `ConfluenceIntegrationConfigDialog` | `src/JiraClone.WinForms/Forms/Integrations/ConfluenceIntegrationConfigDialog.cs` | Standard WinForms controls | `IntegrationSettingsControl` mở từ card Confluence | OK/Cancel rồi `Close()` | KHÔNG TÌM THẤY navigate sang form khác |
| `ConfluencePageLinkDialog` | `src/JiraClone.WinForms/Forms/Integrations/ConfluencePageLinkDialog.cs` | Standard WinForms controls | `IssueIntegrationsControl` mở khi thêm Confluence link | OK/Cancel rồi `Close()` | KHÔNG TÌM THẤY navigate sang form khác |
| `GitHubIntegrationConfigDialog` | `src/JiraClone.WinForms/Forms/Integrations/GitHubIntegrationConfigDialog.cs` | Standard WinForms controls | `IntegrationSettingsControl` mở từ card GitHub | OK/Cancel rồi `Close()` | KHÔNG TÌM THẤY navigate sang form khác |

### Nested form/dialog classes được định nghĩa trong cùng file

| Form/Class | File | Loại rendering | Mở bằng cách nào | Đóng bằng cách nào | Navigate tiếp theo |
|---|---|---|---|---|---|
| `IssueNavigatorForm.SaveFilterDialog` | `src/JiraClone.WinForms/Forms/IssueNavigatorForm.cs` | Standard WinForms controls | Từ `IssueNavigatorForm` khi lưu saved filter | `DialogResult.OK/Cancel` + `Close()` | KHÔNG TÌM THẤY |
| `SprintManagementForm.CreateSprintDialog` | `src/JiraClone.WinForms/Forms/SprintManagementForm.cs` | Standard WinForms controls | `SprintManagementForm.CreateSprintAsync()` | `DialogResult.OK/Cancel` + `Close()` | KHÔNG TÌM THẤY |
| `SprintManagementForm.CloseSprintDialog` | `src/JiraClone.WinForms/Forms/SprintManagementForm.cs` | Standard WinForms controls | `SprintManagementForm.CloseSprintAsync()` | `DialogResult.OK/Cancel` + `Close()` | KHÔNG TÌM THẤY |
| `UserManagementForm.UserEditorDialog` | `src/JiraClone.WinForms/Forms/UserManagementForm.cs` | Standard WinForms controls | `UserManagementForm.CreateAsync()` / `EditAsync()` | Save/Cancel rồi `Close()` | KHÔNG TÌM THẤY |
| `ProjectSettingsForm.DeleteProjectDialog` | `src/JiraClone.WinForms/Forms/ProjectSettingsForm.cs` | Standard WinForms controls | `ProjectSettingsForm.DeleteProjectAsync()` | `DialogResult.OK/Cancel` + `Close()` | KHÔNG TÌM THẤY |
| `ProjectSettingsForm.MemberDialog` | `src/JiraClone.WinForms/Forms/ProjectSettingsForm.cs` | Standard WinForms controls | `ProjectSettingsForm.AddMemberAsync()` | `DialogResult.OK/Cancel` + `Close()` | KHÔNG TÌM THẤY |
| `ProjectSettingsForm.MemberRoleDialog` | `src/JiraClone.WinForms/Forms/ProjectSettingsForm.cs` | Standard WinForms controls | `ProjectSettingsForm.ChangeMemberRoleAsync()` | `DialogResult.OK/Cancel` + `Close()` | KHÔNG TÌM THẤY |
| `ProjectSettingsForm.BoardColumnDialog` | `src/JiraClone.WinForms/Forms/ProjectSettingsForm.cs` | Standard WinForms controls | `ProjectSettingsForm.EditColumnAsync()` | `DialogResult.OK/Cancel` + `Close()` | KHÔNG TÌM THẤY |
| `ProjectSettingsForm.LabelDialog` | `src/JiraClone.WinForms/Forms/ProjectSettingsForm.cs` | Standard WinForms controls | `ProjectSettingsForm.AddLabelAsync()` / `EditLabelAsync()` | `DialogResult.OK/Cancel` + `Close()` | KHÔNG TÌM THẤY |
| `ProjectSettingsForm.ComponentDialog` | `src/JiraClone.WinForms/Forms/ProjectSettingsForm.cs` | Standard WinForms controls | `ProjectSettingsForm.AddComponentAsync()` / `EditComponentAsync()` | `DialogResult.OK/Cancel` + `Close()` | KHÔNG TÌM THẤY |
| `ProjectSettingsForm.VersionDialog` | `src/JiraClone.WinForms/Forms/ProjectSettingsForm.cs` | Standard WinForms controls | `ProjectSettingsForm.AddVersionAsync()` / `EditVersionAsync()` | `DialogResult.OK/Cancel` + `Close()` | KHÔNG TÌM THẤY |
| `IssueDetailsForm.LogTimeDialog` | `src/JiraClone.WinForms/Forms/IssueDetailsForm.cs` | Standard WinForms controls | `IssueDetailsForm.OnLogTimeClick` | `DialogResult.OK/Cancel` + `Close()` | KHÔNG TÌM THẤY |
| `IssueDetailsForm.LabelPickerDialog` | `src/JiraClone.WinForms/Forms/IssueDetailsForm.cs` | Standard WinForms controls | `IssueDetailsForm.OnEditLabelsClick` | `DialogResult.OK/Cancel` + `Close()` | KHÔNG TÌM THẤY |
| `IssueDetailsForm.ExistingIssuePickerDialog` | `src/JiraClone.WinForms/Forms/IssueDetailsForm.cs` | Standard WinForms controls | `IssueDetailsForm.AddExistingChildIssueAsync()` | `DialogResult.OK/Cancel` + `Close()` | KHÔNG TÌM THẤY |
| `IssueDetailsForm.AssigneePickerDialog` | `src/JiraClone.WinForms/Forms/IssueDetailsForm.cs` | Standard WinForms controls | `IssueDetailsForm.OnAssigneeClick` | `DialogResult.OK/Cancel` + `Close()` | KHÔNG TÌM THẤY |

### 1b. Danh sách control tương tác được

#### LoginForm
- `TextBox | _emailTextBox | Placeholder: source khởi tạo bằng code trong constructor, scan nhanh không thấy literal ngay ở field | KHÔNG TÌM THẤY`
- `TextBox | _passwordTextBox | Placeholder: KHÔNG TÌM THẤY | KHÔNG TÌM THẤY`
- `CheckBox | _rememberMeCheckBox | "Remember me for 30 days" | KHÔNG TÌM THẤY`
- `Button | _loginButton | "Log in" | KHÔNG TÌM THẤY`
- `Button | _showPasswordButton | "Show" | KHÔNG TÌM THẤY`
- `Button | _closeButton | "X" | KHÔNG TÌM THẤY`
- `Button | _ssoButton | "Sign in with {provider}" | KHÔNG TÌM THẤY`

#### MainForm
- `TextBox | _searchBox | Placeholder thay đổi theo active nav/project (`GetSearchPlaceholder`) | KHÔNG TÌM THẤY`
- `Button | _logoutButton | text do factory tạo ("Đăng xuất"/"Logout" tùy source hiện tại) | KHÔNG TÌM THẤY`
- `Button | _createIssueButton | "Create" | KHÔNG TÌM THẤY`
- `Button | _cancelButton | "Cancel" | KHÔNG TÌM THẤY`
- `Button | _notificationButton | text rỗng, dùng icon bell | KHÔNG TÌM THẤY`
- `Button | _markAllReadButton | "Mark all as read" | KHÔNG TÌM THẤY`
- `Custom control | _projectSwitcher | project switcher custom control | KHÔNG TÌM THẤY`
- `Custom control | _projectsItem/_dashboardItem/_boardItem/_backlogItem/_roadmapItem/_sprintsItem/_issuesItem/_reportsItem/_usersItem/_settingsItem | sidebar nav custom control | KHÔNG TÌM THẤY`

#### ProjectListForm
- `Button | _createProjectButton | "+ Create Project" | KHÔNG TÌM THẤY`
- `Button | _openProjectButton | "Open Project" | KHÔNG TÌM THẤY`
- `Button | _cardsViewButton | "Cards" | KHÔNG TÌM THẤY`
- `Button | _gridViewButton | "Grid" | KHÔNG TÌM THẤY`
- `ListView | _gridView | columns: Key, Name, Category, Members, Updated (xem ConfigureGrid) | KHÔNG TÌM THẤY`
- `Custom control | ProjectCard (tạo runtime trong cards panel) | card dự án vẽ bằng OnPaint | KHÔNG TÌM THẤY`

#### DashboardForm
- `Button | _refreshButton | "Refresh" | KHÔNG TÌM THẤY`
- `Standard interactive control khác | KHÔNG TÌM THẤY ở field top-level`
- `Custom interactive controls | row/card custom trong activity, assigned issues, team workload | KHÔNG TÌM THẤY`

#### BoardForm
- `Button | _startSprintButton | "Start Sprint" | KHÔNG TÌM THẤY`
- `Button | _boardModeButton | "Mode: Scrum" / được update runtime | KHÔNG TÌM THẤY`
- `ComboBox | _assigneeFilter | item đầu: "All assignees" (nạp runtime) | KHÔNG TÌM THẤY`
- `ComboBox | _priorityFilter | item đầu: "All priorities" (nạp runtime) | KHÔNG TÌM THẤY`
- `ComboBox | _typeFilter | item đầu: "All types" (nạp runtime) | KHÔNG TÌM THẤY`
- `TextBox | _searchFilter | Placeholder: "Search issues" | KHÔNG TÌM THẤY`
- `Button | _groupByEpicButton | "Group by Epic" | KHÔNG TÌM THẤY`
- `Button | _clearFiltersButton | "Clear filters" | KHÔNG TÌM THẤY`
- `Custom control | BoardColumnControl/EpicSwimlaneControl/IssueCardControl | drag-drop + click issue | KHÔNG TÌM THẤY`

#### RoadmapForm
- `ComboBox | _sprintFilter | item đầu: "Tất cả sprint" (nạp runtime) | KHÔNG TÌM THẤY`
- `ComboBox | _assigneeFilter | item đầu: "Tất cả người phụ trách" (nạp runtime) | KHÔNG TÌM THẤY`
- `Button | _refreshButton | "Làm mới" | KHÔNG TÌM THẤY`
- `ListBox | _epicList | owner-draw list epic | KHÔNG TÌM THẤY`
- `Button | _openEpicButton | "Mở epic" | KHÔNG TÌM THẤY`
- `ListView | _childIssuesList | danh sách issue con liên kết | KHÔNG TÌM THẤY`
- `Custom control | _timelineCanvas | canvas timeline kéo-thả lịch epic | KHÔNG TÌM THẤY`

#### SprintManagementForm
- `ListView | _listView | sprint list | KHÔNG TÌM THẤY`
- `Button | _createButton | "Tạo sprint" | KHÔNG TÌM THẤY`
- `Button | _assignButton | "Gán issue" | KHÔNG TÌM THẤY`
- `Button | _startButton | "Bắt đầu sprint" | KHÔNG TÌM THẤY`
- `Button | _closeButton | "Đóng sprint" | KHÔNG TÌM THẤY`

#### IssueNavigatorForm
- `ComboBox | _statusFilter | item đầu nạp runtime | KHÔNG TÌM THẤY`
- `ComboBox | _priorityFilter | item đầu nạp runtime | KHÔNG TÌM THẤY`
- `ComboBox | _typeFilter | item đầu nạp runtime | KHÔNG TÌM THẤY`
- `TextBox | _searchBox | Placeholder trong source hiện tại là chuỗi tiếng Việt đã localize/một số chỗ còn mojibake | KHÔNG TÌM THẤY`
- `Button | _createIssueButton | "Tạo issue" | KHÔNG TÌM THẤY`
- `Button | _runQueryButton | "Chạy truy vấn" | KHÔNG TÌM THẤY`
- `Button | _clearQueryButton | "Xóa" | KHÔNG TÌM THẤY`
- `Button | _saveFilterButton | "Lưu bộ lọc" | KHÔNG TÌM THẤY`
- `Button | _deleteFilterButton | "Xóa bộ lọc" | KHÔNG TÌM THẤY`
- `ListBox | _savedFilters | saved JQL filter list | KHÔNG TÌM THẤY`
- `DataGridView | _grid | issue result grid | KHÔNG TÌM THẤY`
- `Button | _openButton | "Mở issue" | KHÔNG TÌM THẤY`
- `Custom control | _jqlEditor | JQL editor custom control | KHÔNG TÌM THẤY`

#### ReportsForm
- `ComboBox | _sprintSelector | sprint filter | KHÔNG TÌM THẤY`
- `Button | _refreshButton | chuỗi localize hiện tại trong source có chỗ mojibake | KHÔNG TÌM THẤY`
- `Button | _exportButton | "Xuất PNG" | KHÔNG TÌM THẤY`
- `TabControl | _tabs | Burndown / Velocity / CFD / Sprint Report | KHÔNG TÌM THẤY`
- `ComboBox | _sprintReportSelector | sprint report selector | KHÔNG TÌM THẤY`
- `Custom control | BurndownChartPanel / VelocityChartPanel / CfdChartPanel / ReportMetricCard / SprintIssueBucket | KHÔNG TÌM THẤY`

#### UserManagementForm
- `ListView | _listView | user list | KHÔNG TÌM THẤY`
- `TextBox | _searchBox | Placeholder: "Search users" | KHÔNG TÌM THẤY`
- `ComboBox | _statusFilter | items: "All users", "Active only", "Inactive only" | KHÔNG TÌM THẤY`
- `Button | _createButton | "Create" | KHÔNG TÌM THẤY`
- `Button | _editButton | "Edit" | KHÔNG TÌM THẤY`
- `Button | _deactivateButton | "Deactivate" | KHÔNG TÌM THẤY`
- `Button | _activateButton | "Activate" | KHÔNG TÌM THẤY`
- `Button | _resetPasswordButton | "Reset Password" | KHÔNG TÌM THẤY`

#### ProjectSettingsForm
- `TextBox | _name | project name | KHÔNG TÌM THẤY`
- `TextBox | _description | project description | KHÔNG TÌM THẤY`
- `ComboBox | _category | project category | KHÔNG TÌM THẤY`
- `ComboBox | _boardType | board type | KHÔNG TÌM THẤY`
- `TextBox | _url | project URL | KHÔNG TÌM THẤY`
- `ListView | _members | project member list | KHÔNG TÌM THẤY`
- `ListView | _columns | board column list | KHÔNG TÌM THẤY`
- `ListView | _labels | label list | KHÔNG TÌM THẤY`
- `ListView | _components | component list | KHÔNG TÌM THẤY`
- `ListView | _versions | version list | KHÔNG TÌM THẤY`
- `DataGridView | _webhooks | webhook endpoint list | KHÔNG TÌM THẤY`
- `TextBox | _permissionSchemeName | permission scheme name | KHÔNG TÌM THẤY`
- `CheckBox matrix | _permissionChecks[(Permission, ProjectRole)] | permission matrix checkbox per project role | KHÔNG TÌM THẤY`
- `Button | _saveProject | "Save Project" | KHÔNG TÌM THẤY`
- `Button | _saveBoardSettings | "Save Board" | KHÔNG TÌM THẤY`
- `Button | _archiveProject | "Archive Project" | KHÔNG TÌM THẤY`
- `Button | _deleteProject | "Delete Project" | KHÔNG TÌM THẤY`
- `Button | _addMember / _changeMemberRole / _removeMember | member actions | KHÔNG TÌM THẤY`
- `Button | _editColumn | "Edit Column" | KHÔNG TÌM THẤY`
- `Button | _addLabel / _editLabel / _deleteLabel | label actions | KHÔNG TÌM THẤY`
- `Button | _addComponent / _editComponent / _deleteComponent | component actions | KHÔNG TÌM THẤY`
- `Button | _addVersion / _editVersion / _deleteVersion / _markVersionReleased | version actions | KHÔNG TÌM THẤY`
- `Button | _addWebhook / _editWebhook / _deleteWebhook / _testWebhook / _viewWebhookHistory | webhook actions | KHÔNG TÌM THẤY`
- `Button | _savePermissions | "Save Permissions" | KHÔNG TÌM THẤY`
- `Custom controls | _profileSettings / _workflowSettings / _integrationSettings | KHÔNG TÌM THẤY`

#### CreateProjectForm
- `TextBox | _nameTextBox | project name | KHÔNG TÌM THẤY`
- `TextBox | _keyTextBox | project key | KHÔNG TÌM THẤY`
- `ComboBox | _categoryComboBox | project category | KHÔNG TÌM THẤY`
- `TextBox | _descriptionTextBox | description | KHÔNG TÌM THẤY`
- `DataGridView | _memberGrid | invite member grid | KHÔNG TÌM THẤY`
- `Button | _backButton | "Back" | KHÔNG TÌM THẤY`
- `Button | _nextButton | "Next" | KHÔNG TÌM THẤY`
- `Button | _createButton | "Create Project" | KHÔNG TÌM THẤY`
- `Button | _cancelButton | "Cancel" | KHÔNG TÌM THẤY`

#### IssueEditorForm
- `TextBox | _titleTextBox | title | KHÔNG TÌM THẤY`
- `TextBox | _descriptionTextBox | description | KHÔNG TÌM THẤY`
- `ComboBox | _typeComboBox | issue type | KHÔNG TÌM THẤY`
- `ComboBox | _statusComboBox | workflow status | KHÔNG TÌM THẤY`
- `ComboBox | _priorityComboBox | priority | KHÔNG TÌM THẤY`
- `ComboBox | _reporterComboBox | reporter | KHÔNG TÌM THẤY`
- `CheckedListBox | _assigneesList | assignees | KHÔNG TÌM THẤY`
- `SprintSelectorControl | _sprintSelector | sprint selector custom control | KHÔNG TÌM THẤY`
- `DateTimePicker | _dueDatePicker | due date | KHÔNG TÌM THẤY`
- `CheckBox | _noDueDateCheckBox | "No due date" | KHÔNG TÌM THẤY`
- `ComboBox | _parentComboBox | parent / epic link | KHÔNG TÌM THẤY`
- `Button | _saveButton | "Save issue" | KHÔNG TÌM THẤY`
- `Button | _cancelButton | "Cancel" | KHÔNG TÌM THẤY`

#### IssueDetailsForm
- `TextBox | _titleEditor | issue title editor | KHÔNG TÌM THẤY`
- `Button | _descriptionEdit | "Edit" | KHÔNG TÌM THẤY`
- `Button | _descriptionPreview | "Preview" | KHÔNG TÌM THẤY`
- `Button | _commentsTab | "Comments" | KHÔNG TÌM THẤY`
- `Button | _historyTab | "History" | KHÔNG TÌM THẤY`
- `Button | _childIssuesTab | "Child Issues" | KHÔNG TÌM THẤY`
- `TextBox | _commentInput | comment composer | KHÔNG TÌM THẤY`
- `Button | _saveComment | "Save" | KHÔNG TÌM THẤY`
- `Button | _addExistingIssueButton | "Add existing issue" | KHÔNG TÌM THẤY`
- `Button | _createChildIssueButton | "Create child issue" | KHÔNG TÌM THẤY`
- `ComboBox | _status | status dropdown | KHÔNG TÌM THẤY`
- `ComboBox | _priority | priority dropdown | KHÔNG TÌM THẤY`
- `NumericUpDown | _storyPoints | story points | KHÔNG TÌM THẤY`
- `Button | _logTime | "Log Time" | KHÔNG TÌM THẤY`
- `Button | _editLabels | "Edit Labels" | KHÔNG TÌM THẤY`
- `ComboBox | _component | component selector | KHÔNG TÌM THẤY`
- `ComboBox | _fixVersion | fix version selector | KHÔNG TÌM THẤY`
- `LinkLabel | _dueDateDisplay | due date link display | KHÔNG TÌM THẤY`
- `DateTimePicker | _dueDatePicker | due date editor | KHÔNG TÌM THẤY`
- `Button | _watchButton | "Watch" | KHÔNG TÌM THẤY`
- `Button | _delete | "Delete" | KHÔNG TÌM THẤY`
- `LinkLabel | _parentLink | parent issue link | KHÔNG TÌM THẤY`
- `Custom controls | _descriptionEditor / _descriptionViewer / _attachmentPicker / _attachments / _comments / _history / _sprint / _assignee / _reporter / _integrations / _time | KHÔNG TÌM THẤY`

#### CreateApiTokenDialog
- `TextBox | _name | token name | KHÔNG TÌM THẤY`
- `ComboBox | _expiry | expiry | KHÔNG TÌM THẤY`
- `CheckedListBox | _scopes | token scopes | KHÔNG TÌM THẤY`
- `Button | _okButton | "Create Token" | KHÔNG TÌM THẤY`
- `Button | local variable cancelButton | "Cancel" | KHÔNG TÌM THẤY`

#### GeneratedApiTokenDialog
- `TextBox | local variable tokenBox | token raw value | KHÔNG TÌM THẤY`
- `Button | local variable copyButton | "Copy Token" | KHÔNG TÌM THẤY`
- `Button | local variable closeButton | "Close" | KHÔNG TÌM THẤY`

#### ResetPasswordDialog
- `TextBox | _newPassword | new password | KHÔNG TÌM THẤY`
- `TextBox | _confirmPassword | confirm password | KHÔNG TÌM THẤY`
- `Button | _okButton | "Reset Password" | KHÔNG TÌM THẤY`
- `Button | _cancelButton | "Cancel" | KHÔNG TÌM THẤY`

#### WebhookEndpointDialog
- `TextBox | _name | webhook name | KHÔNG TÌM THẤY`
- `TextBox | _url | webhook URL | KHÔNG TÌM THẤY`
- `TextBox | _secret | webhook secret | KHÔNG TÌM THẤY`
- `Button | _generateSecret | "Generate Secret" | KHÔNG TÌM THẤY`
- `CheckBox | _active | "Active" | KHÔNG TÌM THẤY`
- `CheckedListBox | _events | subscribed events | KHÔNG TÌM THẤY`
- `Button | _okButton | "Save" hoặc "Add" (update runtime) | KHÔNG TÌM THẤY`
- `Button | _cancelButton | "Cancel" | KHÔNG TÌM THẤY`

#### WebhookDeliveryHistoryForm
- `DataGridView | _grid | delivery history grid | KHÔNG TÌM THẤY`

#### ConfluenceIntegrationConfigDialog
- `TextBox | _baseUrl | base URL | KHÔNG TÌM THẤY`
- `TextBox | _spaceKey | space key | KHÔNG TÌM THẤY`
- `TextBox | _email | email | KHÔNG TÌM THẤY`
- `TextBox | _apiToken | API token | KHÔNG TÌM THẤY`
- `CheckBox | _enabled | "Enabled" | KHÔNG TÌM THẤY`
- `Button | _ok | "Save" | KHÔNG TÌM THẤY`
- `Button | local variable cancel | "Cancel" | KHÔNG TÌM THẤY`

#### ConfluencePageLinkDialog
- `TextBox | _title | page title | KHÔNG TÌM THẤY`
- `TextBox | _url | page URL | KHÔNG TÌM THẤY`
- `Button | _ok | "Add Link" | KHÔNG TÌM THẤY`
- `Button | local variable cancel | "Cancel" | KHÔNG TÌM THẤY`

#### GitHubIntegrationConfigDialog
- `TextBox | _owner | owner | KHÔNG TÌM THẤY`
- `TextBox | _repo | repository | KHÔNG TÌM THẤY`
- `TextBox | _apiToken | API token | KHÔNG TÌM THẤY`
- `CheckBox | _enabled | "Enabled" | KHÔNG TÌM THẤY`
- `Button | _ok | "Save" | KHÔNG TÌM THẤY`
- `Button | local variable cancel | "Cancel" | KHÔNG TÌM THẤY`

## MỤC 2 — ROLE & PERMISSION MAP

### 2a. Danh sách role hiện có

#### Global role constants (`src/JiraClone.Application/Roles/RoleCatalog.cs`)
- `Admin`
- `ProjectManager`
- `Developer`
- `Viewer`

#### Project role enum (`src/JiraClone.Domain/Enums/ProjectRole.cs`)
- `Viewer = 1`
- `Developer = 2`
- `ProjectManager = 3`
- `Admin = 4`

#### Permission enum (`src/JiraClone.Domain/Enums/Permission.cs`)
- `CreateIssue`
- `EditIssue`
- `DeleteIssue`
- `TransitionIssue`
- `ManageSprints`
- `ManageBoard`
- `ManageProject`
- `ManageMembers`
- `ViewProject`
- `AddComment`
- `EditOwnComment`
- `DeleteOwnComment`

### 2b. Seed user theo role (`src/JiraClone.Persistence/Seed/SeedData.cs`)

| Username | Password (plain text neu co) | Global role | Project duoc assign |
|---|---|---|---|
| `admin` | KHONG TIM THAY trong `SeedData.cs`; chi co `PasswordHash` va `PasswordSalt` | `Admin` | `Jira Clone Migration` (`ProjectId = 1`), `ProjectRole.Admin` |
| `gaben` | KHONG TIM THAY trong `SeedData.cs`; chi co `PasswordHash` va `PasswordSalt` | `Developer` | `Jira Clone Migration` (`ProjectId = 1`), `ProjectRole.Developer` |
| `yoda` | KHONG TIM THAY trong `SeedData.cs`; chi co `PasswordHash` va `PasswordSalt` | `ProjectManager` | `Jira Clone Migration` (`ProjectId = 1`), `ProjectRole.ProjectManager` |

### 2c. Permission theo role

#### Nguon quyen mac dinh theo project role (`src/JiraClone.Domain/Permissions/PermissionDefaults.cs`)

| Project role | Permission duoc grant mac dinh |
|---|---|
| `Viewer` | `ViewProject` |
| `Developer` | `ViewProject`, `CreateIssue`, `EditIssue`, `DeleteIssue`, `TransitionIssue`, `AddComment`, `EditOwnComment`, `DeleteOwnComment` |
| `ProjectManager` | Toan bo quyen cua `Developer` + `ManageSprints`, `ManageBoard`, `ManageProject`, `ManageMembers` |
| `Admin` | Cung tap permission mac dinh nhu `ProjectManager` |

#### Global role check (`src/JiraClone.Application/Roles/AuthorizationService.cs`)
- `IsInRole(params string[] roleNames)` doc role tu `CurrentUserContext.CurrentUser.UserRoles`
- `EnsureInRole(params string[] roleNames)` nem `UnauthorizedAccessException("Current user does not have permission to perform this action.")` neu khong dung role

#### Action bi rang buoc boi project permission
- `BoardQueryService` yeu cau `Permission.ViewProject`
- `RoadmapService` yeu cau `Permission.ViewProject`
- `SprintService.CreateAsync/AssignIssuesAsync/StartSprintAsync/CloseSprintAsync` yeu cau `Permission.ManageSprints`
- `IssueService.CreateAsync` yeu cau `Permission.CreateIssue`
- `IssueService.Update...` yeu cau `Permission.EditIssue`
- `IssueService.Delete...` yeu cau `Permission.DeleteIssue`
- `IssueService` transition yeu cau `Permission.TransitionIssue`
- `ProjectCommandService.SaveProjectAsync/ArchiveProjectAsync/DeleteProjectAsync` yeu cau `Permission.ManageProject`
- `ProjectCommandService.AddMemberAsync/ChangeMemberRoleAsync/RemoveMemberAsync` yeu cau `Permission.ManageMembers`
- `ProjectCommandService.EditBoardColumnAsync` yeu cau `Permission.ManageBoard`
- `WebhookService` yeu cau `Permission.ManageProject`
- `ConfluenceIntegrationService.Configure/Disconnect` yeu cau `Permission.ManageProject`
- `ConfluenceIntegrationService.AddPageLink/CreatePageFromIssue` yeu cau `Permission.EditIssue`
- `GitHubIntegrationService.Configure/Disconnect` yeu cau `Permission.ManageProject`
- `LocalApiServer GET /api/v1/issues` check `Permission.ViewProject`
- `LocalApiServer POST /api/v1/issues` check `Permission.CreateIssue`

#### Action bi rang buoc boi global role (`EnsureInRole`)
- `ProjectCommandService.CreateProjectAsync` yeu cau global role `Admin` hoac `ProjectManager`
- `AttachmentFacade` mutation path yeu cau `Admin` / `ProjectManager` / `Developer`
- `CommentService` mutation path yeu cau `Admin` / `ProjectManager` / `Developer`
- `ComponentService` mutation path yeu cau `Admin` / `ProjectManager`
- `LabelService` mutation path yeu cau `Admin` / `ProjectManager`
- `VersionService` mutation path yeu cau `Admin` / `ProjectManager`
- `SavedFilterService` cho `Admin` / `ProjectManager` / `Developer` / `Viewer`
- `WatcherService` cho `Admin` / `ProjectManager` / `Developer` / `Viewer`

#### Tong hop theo role

##### Viewer
- Duoc lam: xem project qua `Permission.ViewProject`; dung `SavedFilterService`, `WatcherService`; co the duoc provision qua SSO test case `LoginWithSsoAsync_NewUser_ProvisionedWithViewerRole`
- Khong duoc lam: tao/sua/xoa/transition issue; manage sprint/board/project/member; cac service mutation co `EnsureInRole(Admin, ProjectManager, Developer)` se bi block
- UI an/disable theo role: KHONG TIM THAY UI rieng cho `Viewer`; cac nut phu thuoc permission se bi service block neu user van den duoc man hinh

##### Developer
- Duoc lam: theo default permission co the `CreateIssue`, `EditIssue`, `DeleteIssue`, `TransitionIssue`, `AddComment`, `EditOwnComment`, `DeleteOwnComment`, `ViewProject`
- Khong duoc lam: `ManageSprints`, `ManageBoard`, `ManageProject`, `ManageMembers`; khong duoc tao project boi `ProjectCommandService.CreateProjectAsync`
- UI an/disable theo role:
  - `ProjectListForm.UpdateButtonStates()` chi enable `_createProjectButton` cho `Admin`, `ProjectManager`
  - `SprintManagementForm.UpdateActionState()` disable `_createButton`, `_assignButton`, `_startButton`, `_closeButton` neu khong phai `Admin`/`ProjectManager`
  - `BoardForm` chi hien `_boardModeButton` khi `HasPermission(ManageProject)` tra ve `true`
  - `MainForm` khong them `_usersItem` vao sidebar cho `Developer`

##### ProjectManager
- Duoc lam: toan bo quyen cua `Developer` + `ManageSprints`, `ManageBoard`, `ManageProject`, `ManageMembers`
- Khong duoc lam: truy cap `UserManagementForm` theo global role gate hien tai
- UI an/disable theo role:
  - `_createProjectButton` duoc enable trong `ProjectListForm`
  - action sprint duoc enable trong `SprintManagementForm`
  - `WorkflowSettingsControl.UpdateActionState()` enable add/edit/delete status/transition cho `Admin`, `ProjectManager`
  - `_usersItem` van KHONG duoc them vao sidebar vi `MainForm` chi them cho global `Admin`

##### Admin
- Duoc lam: cung tap project permission nhu `ProjectManager`; them vao do co global role `Admin` de vao man user management
- Khong duoc lam: KHONG TIM THAY action nao bi block rieng cho `Admin`
- UI an/disable theo role:
  - `MainForm` chi add `_usersItem` khi `_session.Authorization.IsInRole(RoleCatalog.Admin)`
  - `MainForm.OnUsersItemClick()` return neu current user khong phai `Admin`
  - `UserManagementForm.UpdateActionState()` chi hien/enable `_createButton`, `_editButton`, `_resetPasswordButton`, `_deactivateButton`, `_activateButton` khi `isAdmin == true`

## MUC 3 — APPSESSION & RUNTIME STATE

### 3a. Property luu current user / current role
- `AppSession.CurrentUserContext`
- `CurrentUserContext.CurrentUser`
- `AppSession.cs` KHONG co property `CurrentRole` rieng
- Role hien tai duoc suy ra tu `CurrentUserContext.CurrentUser.UserRoles`
- `AppSession.Authorization` la instance `AuthorizationService` de query `IsInRole(...)`

### 3b. Property luu current project dang active
- `private Project? _activeProject`
- `public Project? ActiveProject => _activeProject`

### 3c. Method login / logout / switch project

#### Login
- `AppSession.Authentication.LoginAsync(string userName, string password, ...)`
- `AppSession.Authentication.LoginWithSsoAsync(string email, string? displayName = null, string? suggestedUserName = null, ...)`
- `AppSession.Authentication.ValidateRefreshTokenAsync(int userId, string refreshToken, ...)`

#### Logout
- Method ten `Logout` trong `AppSession.cs`: KHONG TIM THAY
- Method lien quan logout trong `AppSession.cs`:
  - `AppSession.Authentication.ClearPersistentSessionAsync(int userId, ...)`
  - `CurrentUserContext.Clear()`
- Logout UI thuc te xay ra trong `MainForm.LogoutAsync()`, ngoai `AppSession.cs`

#### Switch project
- `InitializeActiveProjectAsync(...)`
- `SetActiveProjectAsync(int projectId, ...)`
- `RefreshActiveProjectAsync(...)`

### 3d. Event khi role/project thay doi
- Event tim thay trong `AppSession.cs`: `public event EventHandler<ProjectChangedEventArgs>? ProjectChanged;`
- Event role change: KHONG TIM THAY
- `ProjectChangedEventArgs` co:
  - `PreviousProject`
  - `CurrentProject`

### 3e. Form/control lang nghe event nao tu AppSession
- `ProjectSwitcherControl` dang ky `_session.ProjectChanged += HandleSessionProjectChanged;`
- `MainForm` dang ky `_session.ProjectChanged += HandleSessionProjectChanged;`
- `ProjectListForm` dang ky `_session.ProjectChanged += HandleSessionProjectChanged;`
- Form/control khac lang nghe event tu `AppSession`: KHONG TIM THAY trong repo scan

### 3f. Runtime service/object duoc AppSession expose
- `Authorization`
- `Authentication`
- `OAuth`
- `ApiTokens`
- `Projects`
- `ProjectCommands`
- `Permissions`
- `Board`
- `Dashboard`
- `Roadmap`
- `Users`
- `UserCommands`
- `Issues`
- `Jql`
- `SavedFilters`
- `Labels`
- `Components`
- `Versions`
- `Integrations`
- `Webhooks`
- `Watchers`
- `Notifications`
- `Workflows`
- `Comments`
- `Sprints`
- `ActivityLog`
- `Attachments`

## MUC 4 — MAINFORM NAVIGATION STRUCTURE

### 4a. Sidebar menu item list

| Sidebar key/control | Text label trong code | Form/UserControl tuong ung |
|---|---|---|
| `_projectsItem` | `Projects` | `ProjectListForm` |
| `_dashboardItem` | `Dashboard` | `DashboardForm` |
| `_boardItem` | `Board` | `BoardForm(_session, activeSprintOnly: true)` |
| `_backlogItem` | `Backlog` | `BoardForm(_session, activeSprintOnly: false)` |
| `_roadmapItem` | `Roadmap` | `RoadmapForm` |
| `_sprintsItem` | `Sprints` | `SprintManagementForm` |
| `_issuesItem` | `Issues` | `IssueNavigatorForm` |
| `_reportsItem` | `Reports` | `ReportsForm` |
| `_usersItem` | `Users` | `UserManagementForm` |
| `_settingsItem` | `Settings` | `ProjectSettingsForm` |

### 4b. Method navigate den form con
- `NavigateTo(SidebarNavItem navItem, Func<Control> createContent)`
- Cac handler menu:
  - `OnProjectsItemClick`
  - `OnDashboardItemClick`
  - `OnBoardItemClick`
  - `OnBacklogItemClick`
  - `OnRoadmapItemClick`
  - `OnSprintsItemClick`
  - `OnIssuesItemClick`
  - `OnReportsItemClick`
  - `OnUsersItemClick`
  - `OnSettingsItemClick`

### 4c. Cac action o top navbar
- `_breadcrumbLabel`
- `_searchBox`
- `_notificationButton`
- `_notificationBadge`
- `_notificationDropdown`
- `_markAllReadButton`
- `_navbarAvatar`
- `_createIssueButton`
- `_cancelButton`

### 4d. Cac action shell nam ngoai top navbar nhung la thanh phan chinh cua MainForm
- `_projectSwitcher` nam trong sidebar, khong nam top navbar
- `_sidebarAvatar` va `_sidebarUserLabel` nam trong sidebar
- `_logoutButton` nam trong sidebar, khong nam top navbar

### 4e. Menu item nao bi an theo role
- `_usersItem` chi duoc add vao `navItems` khi `_session.Authorization.IsInRole(RoleCatalog.Admin)` tra ve `true`
- `OnUsersItemClick()` cung co guard:
  - neu khong phai `Admin` thi `return`
- Cac sidebar item khac bi an theo role: KHONG TIM THAY

### 4f. Top action nao bi an/doi state theo context
- `_createIssueButton.Visible` chi `true` khi active nav la `Issues` va co `ActiveProject`
- `_cancelButton.Visible` chi `true` khi `_isUiBusy == true`
- `_notificationBadge.Visible` chi `true` khi `unreadCount > 0`

## MUC 5 — DB TEST ISOLATION

### 5a. Connection string hien tai doc tu dau
- `Program.cs` doc connection string bang `configuration.GetConnectionString("Default")`
- Key name: `ConnectionStrings:Default`
- Nguon config duoc nap theo thu tu:
  1. `appsettings.json`
  2. `appsettings.{Environment}.json`
  3. `appsettings.Local.json`
  4. Environment variables prefix `JIRACLONE_`
- `JiraCloneDbContextFactory.cs` cung doc `DOTNET_ENVIRONMENT` / `JIRACLONE_ENVIRONMENT` va `GetConnectionString("Default")`

### 5b. EF DbContext duoc register dang gi
- `services.AddDbContextFactory<JiraCloneDbContext>(options => options.UseSqlServer(connectionString));`
- `services.AddScoped(provider => provider.GetRequiredService<IDbContextFactory<JiraCloneDbContext>>().CreateDbContext());`
- Nghia la app runtime su dung:
  - `IDbContextFactory<JiraCloneDbContext>`
  - them mot scoped `JiraCloneDbContext` duoc tao tu factory

### 5c. Test project dang dung InMemory hay SQL Server that
- `tests/JiraClone.Tests` dung ca hai
- InMemory:
  - `DbContextConcurrencyTests`
  - `AttachmentRepositoryTests`
  - `CommentRepositoryTests`
  - `IssueRepositoryTests`
  - `ProjectRepositoryTests`
  - `SprintRepositoryTests`
  - `UserRepositoryTests`
- SQL Server LocalDB that:
  - `MigrationScriptTests` dung `Server=(localdb)\\MSSQLLocalDB;Database=JiraClone_MigrationScriptTests;...`
  - `SprintConfigurationTests` dung `Server=(localdb)\\mssqllocaldb;Database=JiraCloneModelOnly;...`
- Test project dung SQL Server production connection string tu appsettings: KHONG TIM THAY

### 5d. Co DbContext reset / cleanup method khong
- `EnsureDeleted` / `EnsureCreated` trong test repo scan: KHONG TIM THAY
- `IAsyncLifetime` / fixture cleanup cho database: KHONG TIM THAY
- DB reset/helper trung tam: KHONG TIM THAY

### 5e. Seed data duoc goi o dau
- Model seed duoc apply trong `JiraCloneDbContext.OnModelCreating(...)` bang `Seed.SeedData.Apply(modelBuilder);`
- App runtime goi `migrationContext.Database.Migrate();` trong constructor `AppSession`
- Migration/Model seed se duoc apply khi app startup thanh cong

### 5f. Co flag nao de chay app voi DB rieng cho test khong
- Flag test-specific rieng cho DB trong app/runtime: KHONG TIM THAY
- Bien moi truong co anh huong config:
  - `DOTNET_ENVIRONMENT`
  - `JIRACLONE_ENVIRONMENT`
- Test project tao database rieng bang connection string hardcode LocalDB trong tung file test; khong tim thay mot flag chung o cap app

## MUC 6 — GITHUB ACTIONS COMPATIBILITY CHECK

### 6a. Co file `.github/workflows/*.yml` khong
- Repo scan `.github/workflows`: KHONG TIM THAY

### 6b. Target framework cua WinForms project
- `src/JiraClone.WinForms/JiraClone.WinForms.csproj`
- `TargetFramework`: `net8.0-windows`
- `OutputType`: `WinExe`
- `UseWindowsForms`: `true`

### 6c. Dependency nao yeu cau COM / native Windows DLL
- `MarkdownEditorControl` dung `WebBrowser`
- `MarkdownViewerControl` dung `WebBrowser`
- `DpapiIntegrationConfigProtector` dung DPAPI Windows
- `DpapiSessionPersistenceService` dung DPAPI Windows
- `DpapiWebhookSecretProtector` dung DPAPI Windows
- `System.Windows.Forms` / `net8.0-windows` ban than la Windows-specific
- Trong output test co native SQL Client SNI DLL cho Windows (`Microsoft.Data.SqlClient.SNI.dll`)

### 6d. `LocalApiServer` dung port may? co hardcode khong?
- Co hardcode
- Prefix duoc add trong constructor:
  - `http://127.0.0.1:47892/`
  - `http://localhost:47892/`
- Log startup cung ghi `http://localhost:47892`

### 6e. Startup co yeu cau elevation (UAC) khong?
- `app.manifest`: KHONG TIM THAY trong repo scan
- Code startup explicit yeu cau run as administrator: KHONG TIM THAY

### 6f. Background worker co can network that khong

#### `GitHubIntegrationSyncWorker`
- La singleton duoc resolve luc startup trong `Program.cs`
- Tao `Timer` chay sau `TimeSpan.FromMinutes(1)` va lap lai moi `15` phut
- Moi tick tao scope va resolve `IGitHubIntegrationService`, sau do goi `SyncAllAsync()`
- Mock/test rieng cho `GitHubIntegrationSyncWorker`: KHONG TIM THAY trong repo scan

#### `WebhookDispatcher`
- Dung `HttpClient` that de gui outbound HTTP POST
- Dung queue noi bo (`Channel<WebhookJob>`) + retry
- Co test rieng trong `tests/JiraClone.Tests/Infrastructure/Webhooks/WebhookDispatcherTests.cs`
- Test hien co dung `StubHttpMessageHandler` trong `DispatcherHarness`

#### `LocalApiServer`
- Dung `HttpListener`
- Chi bind loopback local (`127.0.0.1`, `localhost`)
- Endpoint:
  - `GET /api/v1/issues`
  - `POST /api/v1/issues`

## MUC 7 — EXISTING TEST STRUCTURE

### 7a. Cau truc thu muc test hien tai

| Thu muc | Noi dung scan thay |
|---|---|
| `tests/JiraClone.Tests/Application` | service/application tests |
| `tests/JiraClone.Tests/Domain` | domain entity tests |
| `tests/JiraClone.Tests/Infrastructure/Auth` | OAuth/auth infrastructure test |
| `tests/JiraClone.Tests/Infrastructure/Webhooks` | webhook dispatcher tests |
| `tests/JiraClone.Tests/Integration` | integration/smoke/concurrency/migration/session tests |
| `tests/JiraClone.Tests/Persistence` | repository/model configuration tests |

### 7b. Cac test class dang co

#### Application
- `ApiTokenServiceTests` — create/validate/revoke API token
- `AuthenticationServiceTests` — login, SSO provisioning, refresh token, password validation
- `BoardQueryServiceTests` — board query data shape, permission gate
- `IssueServiceTests` — create/update/delete/transition issue, validation, permission behavior
- `JqlParserTests` — JQL parser
- `NotificationServiceTests` — notification behavior
- `ProjectCommandServiceTests` — create/save/archive/delete project, member operations, permission/authorization interactions
- `RoadmapServiceTests` — roadmap DTO/query behavior + permission gate
- `RoleCatalogTests` — role constant values
- `SprintServiceTests` — sprint create/start/assign/close/report behavior + permission behavior
- `UserCommandServiceTests` — user create/update/password rules
- `WebhookServiceTests` — webhook CRUD/send-test behavior + permission behavior

#### Domain
- `IssueTests` — entity/domain rule tests cho `Issue`

#### Infrastructure
- `OAuthTokenValidationTests` — OAuth token validation
- `WebhookDispatcherTests` — queue, timeout, retry, drop-oldest behavior cua dispatcher

#### Integration
- `ApiTokenSmokeTests` — local API server + bearer token smoke path
- `DbContextConcurrencyTests` — EF DbContext concurrency behavior
- `FileShareAttachmentServiceTests` — file attachment storage behavior
- `MigrationScriptTests` — migration SQL / model apply path tren LocalDB
- `SessionPersistenceServiceTests` — session persistence behavior
- `desktop-smoke-checklist.md` — markdown checklist, khong phai test class

#### Persistence
- `AttachmentRepositoryTests`
- `CommentRepositoryTests`
- `IssueRepositoryTests`
- `ProjectRepositoryTests`
- `SprintConfigurationTests`
- `SprintRepositoryTests`
- `UserRepositoryTests`

### 7c. Co base class / fixture chung khong
- Base test class dung chung: KHONG TIM THAY
- `IClassFixture<>`: KHONG TIM THAY
- Collection fixture tim thay:
  - `[CollectionDefinition("LocalApiServer", DisableParallelization = true)]` trong `ApiTokenSmokeTests.cs`
  - `[Collection("LocalApiServer")]` trong `ApiTokenSmokeTests`

### 7d. Co helper tao mock user / mock project khong
- Helper trung tam dung chung cho toan test suite: KHONG TIM THAY
- Helper cuc bo theo file test co tim thay:
  - `SeedUser`
  - `SeedUsers`
  - `SeedProjectGraph`
  - `SeedUserGraph`
  - `SeedAsync`
- Mock object lap lai trong nhieu test:
  - `Mock<ICurrentUserContext>`
  - `Mock<IPermissionService>`
  - `Mock<IAuthorizationService>`

### 7e. Test nao lien quan den auth / permission / role
- `AuthenticationServiceTests`
- `RoleCatalogTests`
- `BoardQueryServiceTests` (mock `IPermissionService`, `Permission.ViewProject`)
- `IssueServiceTests` (mock `IPermissionService`)
- `ProjectCommandServiceTests` (mock `IAuthorizationService`, `IPermissionService`)
- `RoadmapServiceTests` (mock `IPermissionService`, `Permission.ViewProject`)
- `SprintServiceTests` (mock `IPermissionService`)
- `UserCommandServiceTests` (role/password path)
- `WebhookServiceTests` (mock `IPermissionService`, `Permission.ManageProject`)
- `ApiTokenServiceTests` (revoke unauthorized path)
- `ApiTokenSmokeTests` (bearer auth to `LocalApiServer`)

### 7f. Ghi chu thuc trang test isolation
- Nhieu repository/application tests tao `UseInMemoryDatabase(Guid.NewGuid().ToString())`
- Mot so integration/model tests dung SQL Server LocalDB that
- Cleanup/reset DB test cap suite: KHONG TIM THAY
