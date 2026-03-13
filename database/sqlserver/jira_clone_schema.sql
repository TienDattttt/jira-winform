/*
  Jira Clone SQL Server schema
  Target: SQL Server 2019/2022 compatible T-SQL
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE dbo.Roles (
    id INT IDENTITY(1,1) NOT NULL,
    name NVARCHAR(100) NOT NULL,
    description NVARCHAR(250) NULL,
    created_at DATETIME2(7) NOT NULL CONSTRAINT DF_Roles_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(7) NOT NULL CONSTRAINT DF_Roles_updated_at DEFAULT SYSUTCDATETIME(),
    is_deleted BIT NOT NULL CONSTRAINT DF_Roles_is_deleted DEFAULT 0,
    CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (id)
);
GO
CREATE UNIQUE INDEX UX_Roles_name ON dbo.Roles(name) WHERE is_deleted = 0;
GO

CREATE TABLE dbo.Users (
    id INT IDENTITY(1,1) NOT NULL,
    user_name NVARCHAR(100) NOT NULL,
    display_name NVARCHAR(150) NOT NULL,
    email NVARCHAR(200) NOT NULL,
    password_hash NVARCHAR(512) NOT NULL,
    password_salt NVARCHAR(512) NOT NULL,
    avatar_path NVARCHAR(500) NULL,
    is_active BIT NOT NULL CONSTRAINT DF_Users_is_active DEFAULT 1,
    created_at DATETIME2(7) NOT NULL CONSTRAINT DF_Users_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(7) NOT NULL CONSTRAINT DF_Users_updated_at DEFAULT SYSUTCDATETIME(),
    is_deleted BIT NOT NULL CONSTRAINT DF_Users_is_deleted DEFAULT 0,
    CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (id)
);
GO
CREATE UNIQUE INDEX UX_Users_user_name ON dbo.Users(user_name) WHERE is_deleted = 0;
CREATE UNIQUE INDEX UX_Users_email ON dbo.Users(email) WHERE is_deleted = 0;
GO

CREATE TABLE dbo.UserRoles (
    id INT IDENTITY(1,1) NOT NULL,
    user_id INT NOT NULL,
    role_id INT NOT NULL,
    created_at DATETIME2(7) NOT NULL CONSTRAINT DF_UserRoles_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(7) NOT NULL CONSTRAINT DF_UserRoles_updated_at DEFAULT SYSUTCDATETIME(),
    is_deleted BIT NOT NULL CONSTRAINT DF_UserRoles_is_deleted DEFAULT 0,
    CONSTRAINT PK_UserRoles PRIMARY KEY CLUSTERED (id),
    CONSTRAINT FK_UserRoles_Users_user_id FOREIGN KEY (user_id) REFERENCES dbo.Users(id),
    CONSTRAINT FK_UserRoles_Roles_role_id FOREIGN KEY (role_id) REFERENCES dbo.Roles(id),
    CONSTRAINT CK_UserRoles_not_same_delete CHECK (is_deleted IN (0, 1))
);
GO
CREATE UNIQUE INDEX UX_UserRoles_user_id_role_id ON dbo.UserRoles(user_id, role_id) WHERE is_deleted = 0;
GO

CREATE TABLE dbo.Projects (
    id INT IDENTITY(1,1) NOT NULL,
    project_key NVARCHAR(20) NOT NULL,
    name NVARCHAR(200) NOT NULL,
    description NVARCHAR(2000) NULL,
    category NVARCHAR(50) NOT NULL,
    url NVARCHAR(500) NULL,
    is_active BIT NOT NULL CONSTRAINT DF_Projects_is_active DEFAULT 1,
    created_at DATETIME2(7) NOT NULL CONSTRAINT DF_Projects_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(7) NOT NULL CONSTRAINT DF_Projects_updated_at DEFAULT SYSUTCDATETIME(),
    is_deleted BIT NOT NULL CONSTRAINT DF_Projects_is_deleted DEFAULT 0,
    CONSTRAINT PK_Projects PRIMARY KEY CLUSTERED (id)
);
GO
CREATE UNIQUE INDEX UX_Projects_project_key ON dbo.Projects(project_key) WHERE is_deleted = 0;
GO

CREATE TABLE dbo.ProjectMembers (
    id INT IDENTITY(1,1) NOT NULL,
    project_id INT NOT NULL,
    user_id INT NOT NULL,
    project_role NVARCHAR(50) NOT NULL,
    joined_at DATETIME2(7) NOT NULL CONSTRAINT DF_ProjectMembers_joined_at DEFAULT SYSUTCDATETIME(),
    created_at DATETIME2(7) NOT NULL CONSTRAINT DF_ProjectMembers_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(7) NOT NULL CONSTRAINT DF_ProjectMembers_updated_at DEFAULT SYSUTCDATETIME(),
    is_deleted BIT NOT NULL CONSTRAINT DF_ProjectMembers_is_deleted DEFAULT 0,
    CONSTRAINT PK_ProjectMembers PRIMARY KEY CLUSTERED (id),
    CONSTRAINT FK_ProjectMembers_Projects_project_id FOREIGN KEY (project_id) REFERENCES dbo.Projects(id),
    CONSTRAINT FK_ProjectMembers_Users_user_id FOREIGN KEY (user_id) REFERENCES dbo.Users(id)
);
GO
CREATE UNIQUE INDEX UX_ProjectMembers_project_id_user_id ON dbo.ProjectMembers(project_id, user_id) WHERE is_deleted = 0;
CREATE INDEX IX_ProjectMembers_user_id ON dbo.ProjectMembers(user_id);
CREATE INDEX IX_ProjectMembers_project_id_project_role ON dbo.ProjectMembers(project_id, project_role);
GO

CREATE TABLE dbo.IssueTypes (
    id INT IDENTITY(1,1) NOT NULL,
    code NVARCHAR(50) NOT NULL,
    name NVARCHAR(100) NOT NULL,
    description NVARCHAR(250) NULL,
    sort_order INT NOT NULL,
    created_at DATETIME2(7) NOT NULL CONSTRAINT DF_IssueTypes_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(7) NOT NULL CONSTRAINT DF_IssueTypes_updated_at DEFAULT SYSUTCDATETIME(),
    is_deleted BIT NOT NULL CONSTRAINT DF_IssueTypes_is_deleted DEFAULT 0,
    CONSTRAINT PK_IssueTypes PRIMARY KEY CLUSTERED (id)
);
GO
CREATE UNIQUE INDEX UX_IssueTypes_code ON dbo.IssueTypes(code) WHERE is_deleted = 0;
GO

CREATE TABLE dbo.IssueStatuses (
    id INT IDENTITY(1,1) NOT NULL,
    code NVARCHAR(50) NOT NULL,
    name NVARCHAR(100) NOT NULL,
    status_category NVARCHAR(50) NOT NULL,
    sort_order INT NOT NULL,
    created_at DATETIME2(7) NOT NULL CONSTRAINT DF_IssueStatuses_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(7) NOT NULL CONSTRAINT DF_IssueStatuses_updated_at DEFAULT SYSUTCDATETIME(),
    is_deleted BIT NOT NULL CONSTRAINT DF_IssueStatuses_is_deleted DEFAULT 0,
    CONSTRAINT PK_IssueStatuses PRIMARY KEY CLUSTERED (id)
);
GO
CREATE UNIQUE INDEX UX_IssueStatuses_code ON dbo.IssueStatuses(code) WHERE is_deleted = 0;
GO

CREATE TABLE dbo.Priorities (
    id INT IDENTITY(1,1) NOT NULL,
    code NVARCHAR(50) NOT NULL,
    name NVARCHAR(100) NOT NULL,
    weight INT NOT NULL,
    color_hex NVARCHAR(20) NULL,
    sort_order INT NOT NULL,
    created_at DATETIME2(7) NOT NULL CONSTRAINT DF_Priorities_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(7) NOT NULL CONSTRAINT DF_Priorities_updated_at DEFAULT SYSUTCDATETIME(),
    is_deleted BIT NOT NULL CONSTRAINT DF_Priorities_is_deleted DEFAULT 0,
    CONSTRAINT PK_Priorities PRIMARY KEY CLUSTERED (id)
);
GO
CREATE UNIQUE INDEX UX_Priorities_code ON dbo.Priorities(code) WHERE is_deleted = 0;
GO

CREATE TABLE dbo.BoardColumns (
    id INT IDENTITY(1,1) NOT NULL,
    project_id INT NOT NULL,
    issue_status_id INT NOT NULL,
    name NVARCHAR(100) NOT NULL,
    display_order INT NOT NULL,
    wip_limit INT NULL,
    created_at DATETIME2(7) NOT NULL CONSTRAINT DF_BoardColumns_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(7) NOT NULL CONSTRAINT DF_BoardColumns_updated_at DEFAULT SYSUTCDATETIME(),
    is_deleted BIT NOT NULL CONSTRAINT DF_BoardColumns_is_deleted DEFAULT 0,
    CONSTRAINT PK_BoardColumns PRIMARY KEY CLUSTERED (id),
    CONSTRAINT FK_BoardColumns_Projects_project_id FOREIGN KEY (project_id) REFERENCES dbo.Projects(id),
    CONSTRAINT FK_BoardColumns_IssueStatuses_issue_status_id FOREIGN KEY (issue_status_id) REFERENCES dbo.IssueStatuses(id),
    CONSTRAINT CK_BoardColumns_wip_limit CHECK (wip_limit IS NULL OR wip_limit > 0)
);
GO
CREATE UNIQUE INDEX UX_BoardColumns_project_id_issue_status_id ON dbo.BoardColumns(project_id, issue_status_id) WHERE is_deleted = 0;
CREATE INDEX IX_BoardColumns_project_id_display_order ON dbo.BoardColumns(project_id, display_order);
GO

CREATE TABLE dbo.Sprints (
    id INT IDENTITY(1,1) NOT NULL,
    project_id INT NOT NULL,
    name NVARCHAR(200) NOT NULL,
    goal NVARCHAR(1000) NULL,
    start_date DATE NULL,
    end_date DATE NULL,
    sprint_state NVARCHAR(50) NOT NULL,
    closed_at DATETIME2(7) NULL,
    created_at DATETIME2(7) NOT NULL CONSTRAINT DF_Sprints_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(7) NOT NULL CONSTRAINT DF_Sprints_updated_at DEFAULT SYSUTCDATETIME(),
    is_deleted BIT NOT NULL CONSTRAINT DF_Sprints_is_deleted DEFAULT 0,
    CONSTRAINT PK_Sprints PRIMARY KEY CLUSTERED (id),
    CONSTRAINT FK_Sprints_Projects_project_id FOREIGN KEY (project_id) REFERENCES dbo.Projects(id),
    CONSTRAINT CK_Sprints_dates CHECK (start_date IS NULL OR end_date IS NULL OR start_date <= end_date)
);
GO
CREATE INDEX IX_Sprints_project_id_sprint_state ON dbo.Sprints(project_id, sprint_state);
CREATE INDEX IX_Sprints_project_id_start_date ON dbo.Sprints(project_id, start_date);
GO

CREATE TABLE dbo.Issues (
    id INT IDENTITY(1,1) NOT NULL,
    project_id INT NOT NULL,
    sprint_id INT NULL,
    issue_key NVARCHAR(40) NOT NULL,
    title NVARCHAR(200) NOT NULL,
    description_html NVARCHAR(MAX) NULL,
    description_text NVARCHAR(MAX) NULL,
    issue_type_id INT NOT NULL,
    issue_status_id INT NOT NULL,
    priority_id INT NOT NULL,
    reporter_id INT NOT NULL,
    created_by_id INT NOT NULL,
    estimate_hours INT NULL,
    time_spent_hours INT NULL,
    time_remaining_hours INT NULL,
    story_points INT NULL,
    due_date DATE NULL,
    board_position DECIMAL(18,4) NOT NULL CONSTRAINT DF_Issues_board_position DEFAULT (1),
    row_version ROWVERSION NOT NULL,
    created_at DATETIME2(7) NOT NULL CONSTRAINT DF_Issues_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(7) NOT NULL CONSTRAINT DF_Issues_updated_at DEFAULT SYSUTCDATETIME(),
    is_deleted BIT NOT NULL CONSTRAINT DF_Issues_is_deleted DEFAULT 0,
    CONSTRAINT PK_Issues PRIMARY KEY CLUSTERED (id),
    CONSTRAINT FK_Issues_Projects_project_id FOREIGN KEY (project_id) REFERENCES dbo.Projects(id),
    CONSTRAINT FK_Issues_Sprints_sprint_id FOREIGN KEY (sprint_id) REFERENCES dbo.Sprints(id),
    CONSTRAINT FK_Issues_IssueTypes_issue_type_id FOREIGN KEY (issue_type_id) REFERENCES dbo.IssueTypes(id),
    CONSTRAINT FK_Issues_IssueStatuses_issue_status_id FOREIGN KEY (issue_status_id) REFERENCES dbo.IssueStatuses(id),
    CONSTRAINT FK_Issues_Priorities_priority_id FOREIGN KEY (priority_id) REFERENCES dbo.Priorities(id),
    CONSTRAINT FK_Issues_Users_reporter_id FOREIGN KEY (reporter_id) REFERENCES dbo.Users(id),
    CONSTRAINT FK_Issues_Users_created_by_id FOREIGN KEY (created_by_id) REFERENCES dbo.Users(id),
    CONSTRAINT CK_Issues_hours_nonnegative CHECK (
        (estimate_hours IS NULL OR estimate_hours >= 0) AND
        (time_spent_hours IS NULL OR time_spent_hours >= 0) AND
        (time_remaining_hours IS NULL OR time_remaining_hours >= 0) AND
        (story_points IS NULL OR story_points >= 0)
    )
);
GO
CREATE UNIQUE INDEX UX_Issues_project_id_issue_key ON dbo.Issues(project_id, issue_key) WHERE is_deleted = 0;
CREATE INDEX IX_Issues_project_id_issue_status_id_board_position ON dbo.Issues(project_id, issue_status_id, board_position);
CREATE INDEX IX_Issues_project_id_sprint_id_issue_status_id ON dbo.Issues(project_id, sprint_id, issue_status_id);
CREATE INDEX IX_Issues_reporter_id ON dbo.Issues(reporter_id);
CREATE INDEX IX_Issues_updated_at ON dbo.Issues(updated_at);
GO

CREATE TABLE dbo.IssueAssignees (
    id INT IDENTITY(1,1) NOT NULL,
    issue_id INT NOT NULL,
    user_id INT NOT NULL,
    assigned_at DATETIME2(7) NOT NULL CONSTRAINT DF_IssueAssignees_assigned_at DEFAULT SYSUTCDATETIME(),
    created_at DATETIME2(7) NOT NULL CONSTRAINT DF_IssueAssignees_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(7) NOT NULL CONSTRAINT DF_IssueAssignees_updated_at DEFAULT SYSUTCDATETIME(),
    is_deleted BIT NOT NULL CONSTRAINT DF_IssueAssignees_is_deleted DEFAULT 0,
    CONSTRAINT PK_IssueAssignees PRIMARY KEY CLUSTERED (id),
    CONSTRAINT FK_IssueAssignees_Issues_issue_id FOREIGN KEY (issue_id) REFERENCES dbo.Issues(id),
    CONSTRAINT FK_IssueAssignees_Users_user_id FOREIGN KEY (user_id) REFERENCES dbo.Users(id)
);
GO
CREATE UNIQUE INDEX UX_IssueAssignees_issue_id_user_id ON dbo.IssueAssignees(issue_id, user_id) WHERE is_deleted = 0;
CREATE INDEX IX_IssueAssignees_user_id ON dbo.IssueAssignees(user_id);
CREATE INDEX IX_IssueAssignees_issue_id_assigned_at ON dbo.IssueAssignees(issue_id, assigned_at);
GO

CREATE TABLE dbo.Comments (
    id INT IDENTITY(1,1) NOT NULL,
    issue_id INT NOT NULL,
    user_id INT NOT NULL,
    body NVARCHAR(MAX) NOT NULL,
    row_version ROWVERSION NOT NULL,
    created_at DATETIME2(7) NOT NULL CONSTRAINT DF_Comments_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(7) NOT NULL CONSTRAINT DF_Comments_updated_at DEFAULT SYSUTCDATETIME(),
    is_deleted BIT NOT NULL CONSTRAINT DF_Comments_is_deleted DEFAULT 0,
    CONSTRAINT PK_Comments PRIMARY KEY CLUSTERED (id),
    CONSTRAINT FK_Comments_Issues_issue_id FOREIGN KEY (issue_id) REFERENCES dbo.Issues(id),
    CONSTRAINT FK_Comments_Users_user_id FOREIGN KEY (user_id) REFERENCES dbo.Users(id)
);
GO
CREATE INDEX IX_Comments_issue_id_created_at ON dbo.Comments(issue_id, created_at);
CREATE INDEX IX_Comments_user_id ON dbo.Comments(user_id);
GO

CREATE TABLE dbo.Attachments (
    id INT IDENTITY(1,1) NOT NULL,
    issue_id INT NOT NULL,
    stored_file_name NVARCHAR(260) NOT NULL,
    original_file_name NVARCHAR(260) NOT NULL,
    content_type NVARCHAR(150) NOT NULL,
    file_extension NVARCHAR(20) NOT NULL,
    file_size_bytes BIGINT NOT NULL,
    storage_path NVARCHAR(500) NOT NULL,
    uploaded_by_id INT NOT NULL,
    uploaded_at DATETIME2(7) NOT NULL CONSTRAINT DF_Attachments_uploaded_at DEFAULT SYSUTCDATETIME(),
    checksum_sha256 NVARCHAR(64) NOT NULL,
    created_at DATETIME2(7) NOT NULL CONSTRAINT DF_Attachments_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(7) NOT NULL CONSTRAINT DF_Attachments_updated_at DEFAULT SYSUTCDATETIME(),
    is_deleted BIT NOT NULL CONSTRAINT DF_Attachments_is_deleted DEFAULT 0,
    CONSTRAINT PK_Attachments PRIMARY KEY CLUSTERED (id),
    CONSTRAINT FK_Attachments_Issues_issue_id FOREIGN KEY (issue_id) REFERENCES dbo.Issues(id),
    CONSTRAINT FK_Attachments_Users_uploaded_by_id FOREIGN KEY (uploaded_by_id) REFERENCES dbo.Users(id),
    CONSTRAINT CK_Attachments_file_size_bytes CHECK (file_size_bytes >= 0)
);
GO
CREATE INDEX IX_Attachments_issue_id_uploaded_at ON dbo.Attachments(issue_id, uploaded_at);
CREATE INDEX IX_Attachments_uploaded_by_id ON dbo.Attachments(uploaded_by_id);
CREATE INDEX IX_Attachments_checksum_sha256 ON dbo.Attachments(checksum_sha256);
GO

CREATE TABLE dbo.ActivityLogs (
    id INT IDENTITY(1,1) NOT NULL,
    project_id INT NOT NULL,
    issue_id INT NULL,
    user_id INT NOT NULL,
    action_type NVARCHAR(100) NOT NULL,
    field_name NVARCHAR(150) NULL,
    old_value NVARCHAR(2000) NULL,
    new_value NVARCHAR(2000) NULL,
    occurred_at DATETIME2(7) NOT NULL CONSTRAINT DF_ActivityLogs_occurred_at DEFAULT SYSUTCDATETIME(),
    metadata_json NVARCHAR(MAX) NULL,
    created_at DATETIME2(7) NOT NULL CONSTRAINT DF_ActivityLogs_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(7) NOT NULL CONSTRAINT DF_ActivityLogs_updated_at DEFAULT SYSUTCDATETIME(),
    is_deleted BIT NOT NULL CONSTRAINT DF_ActivityLogs_is_deleted DEFAULT 0,
    CONSTRAINT PK_ActivityLogs PRIMARY KEY CLUSTERED (id),
    CONSTRAINT FK_ActivityLogs_Projects_project_id FOREIGN KEY (project_id) REFERENCES dbo.Projects(id),
    CONSTRAINT FK_ActivityLogs_Issues_issue_id FOREIGN KEY (issue_id) REFERENCES dbo.Issues(id),
    CONSTRAINT FK_ActivityLogs_Users_user_id FOREIGN KEY (user_id) REFERENCES dbo.Users(id)
);
GO
CREATE INDEX IX_ActivityLogs_project_id_occurred_at ON dbo.ActivityLogs(project_id, occurred_at);
CREATE INDEX IX_ActivityLogs_issue_id_occurred_at ON dbo.ActivityLogs(issue_id, occurred_at);
CREATE INDEX IX_ActivityLogs_user_id ON dbo.ActivityLogs(user_id);
GO

SET IDENTITY_INSERT dbo.Roles ON;
INSERT INTO dbo.Roles (id, name, description, created_at, updated_at, is_deleted) VALUES
(1, N'Admin', N'Full system access', SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
(2, N'ProjectManager', N'Project administration access', SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
(3, N'Developer', N'Issue editing access', SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
(4, N'Viewer', N'Read-only access', SYSUTCDATETIME(), SYSUTCDATETIME(), 0);
SET IDENTITY_INSERT dbo.Roles OFF;
GO

SET IDENTITY_INSERT dbo.IssueTypes ON;
INSERT INTO dbo.IssueTypes (id, code, name, description, sort_order, created_at, updated_at, is_deleted) VALUES
(1, N'task', N'Task', N'A task represents work that needs to be done.', 1, SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
(2, N'bug', N'Bug', N'A bug represents a defect or malfunction.', 2, SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
(3, N'story', N'Story', N'A story represents user-facing functionality.', 3, SYSUTCDATETIME(), SYSUTCDATETIME(), 0);
SET IDENTITY_INSERT dbo.IssueTypes OFF;
GO

SET IDENTITY_INSERT dbo.IssueStatuses ON;
INSERT INTO dbo.IssueStatuses (id, code, name, status_category, sort_order, created_at, updated_at, is_deleted) VALUES
(1, N'backlog', N'Backlog', N'todo', 1, SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
(2, N'selected', N'Selected', N'todo', 2, SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
(3, N'in_progress', N'In Progress', N'active', 3, SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
(4, N'done', N'Done', N'done', 4, SYSUTCDATETIME(), SYSUTCDATETIME(), 0);
SET IDENTITY_INSERT dbo.IssueStatuses OFF;
GO

SET IDENTITY_INSERT dbo.Priorities ON;
INSERT INTO dbo.Priorities (id, code, name, weight, color_hex, sort_order, created_at, updated_at, is_deleted) VALUES
(1, N'lowest', N'Lowest', 1, N'#66B966', 1, SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
(2, N'low', N'Low', 2, N'#008A00', 2, SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
(3, N'medium', N'Medium', 3, N'#FF9900', 3, SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
(4, N'high', N'High', 4, N'#F06666', 4, SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
(5, N'highest', N'Highest', 5, N'#E60000', 5, SYSUTCDATETIME(), SYSUTCDATETIME(), 0);
SET IDENTITY_INSERT dbo.Priorities OFF;
GO
