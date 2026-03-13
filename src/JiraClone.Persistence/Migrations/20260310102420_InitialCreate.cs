using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace JiraClone.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    PasswordSalt = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    AvatarPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BoardColumns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    WipLimit = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardColumns_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sprints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Goal = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sprints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sprints_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMembers",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ProjectRole = table.Column<int>(type: "int", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMembers", x => new { x.ProjectId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ProjectMembers_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Issues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    SprintId = table.Column<int>(type: "int", nullable: true),
                    IssueKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DescriptionHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ReporterId = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    EstimateHours = table.Column<int>(type: "int", nullable: true),
                    TimeSpentHours = table.Column<int>(type: "int", nullable: true),
                    TimeRemainingHours = table.Column<int>(type: "int", nullable: true),
                    StoryPoints = table.Column<int>(type: "int", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BoardPosition = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Issues_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Issues_Sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "Sprints",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Issues_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Issues_Users_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    IssueId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityLogs_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ActivityLogs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivityLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IssueId = table.Column<int>(type: "int", nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UploadedById = table.Column<int>(type: "int", nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachments_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Attachments_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IssueId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comments_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Comments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IssueAssignees",
                columns: table => new
                {
                    IssueId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueAssignees", x => new { x.IssueId, x.UserId });
                    table.ForeignKey(
                        name: "FK_IssueAssignees_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssueAssignees_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Projects",
                columns: new[] { "Id", "Category", "CreatedAtUtc", "Description", "IsActive", "Key", "Name", "UpdatedAtUtc", "Url" },
                values: new object[] { 1, 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "WinForms migration workspace", true, "JIRA", "Jira Clone Migration", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "https://localhost/jira-clone" });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAtUtc", "Description", "Name", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "Full system access", "Admin", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "Project administration access", "ProjectManager", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "Issue editing access", "Developer", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "Read-only access", "Viewer", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "AvatarPath", "CreatedAtUtc", "DisplayName", "Email", "IsActive", "PasswordHash", "PasswordSalt", "UpdatedAtUtc", "UserName" },
                values: new object[,]
                {
                    { 1, null, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "Admin User", "admin@jiraclone.local", true, "kgu+8rnxTM1wAdC5cmxrD58zMWHiBN9ocgia5jcEKlU=", "E8M6vZg6o0mM1k3m8Xx3MQ==", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "admin" },
                    { 2, null, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "Gaben", "gaben@jiraclone.local", true, "kgu+8rnxTM1wAdC5cmxrD58zMWHiBN9ocgia5jcEKlU=", "E8M6vZg6o0mM1k3m8Xx3MQ==", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "gaben" },
                    { 3, null, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "Yoda", "yoda@jiraclone.local", true, "kgu+8rnxTM1wAdC5cmxrD58zMWHiBN9ocgia5jcEKlU=", "E8M6vZg6o0mM1k3m8Xx3MQ==", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "yoda" }
                });

            migrationBuilder.InsertData(
                table: "BoardColumns",
                columns: new[] { "Id", "CreatedAtUtc", "DisplayOrder", "Name", "ProjectId", "StatusCode", "UpdatedAtUtc", "WipLimit" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1, "Backlog", 1, 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 2, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 2, "Selected", 1, 2, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 3, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 3, "In Progress", 1, 3, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 4, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 4, "Done", 1, 4, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), null }
                });

            migrationBuilder.InsertData(
                table: "ProjectMembers",
                columns: new[] { "ProjectId", "UserId", "JoinedAtUtc", "ProjectRole" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 4 },
                    { 1, 2, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 2 },
                    { 1, 3, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 3 }
                });

            migrationBuilder.InsertData(
                table: "Sprints",
                columns: new[] { "Id", "ClosedAtUtc", "CreatedAtUtc", "EndDate", "Goal", "Name", "ProjectId", "StartDate", "State", "UpdatedAtUtc" },
                values: new object[] { 1, null, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 3, 24), "Seed the desktop migration baseline", "Sprint 1", 1, new DateOnly(2026, 3, 10), 2, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "RoleId", "UserId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 3, 2 },
                    { 2, 3 }
                });

            migrationBuilder.InsertData(
                table: "Issues",
                columns: new[] { "Id", "BoardPosition", "CreatedAtUtc", "CreatedById", "DescriptionHtml", "DescriptionText", "DueDate", "EstimateHours", "IssueKey", "Priority", "ProjectId", "ReporterId", "SprintId", "Status", "StoryPoints", "TimeRemainingHours", "TimeSpentHours", "Title", "Type", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 1, 1m, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1, "Build the initial desktop solution.", "Build the initial desktop solution.", new DateOnly(2026, 3, 20), 8, "JIRA-1", 4, 1, 3, 1, 1, 5, 6, 2, "Set up WinForms solution skeleton", 3, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, 1m, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1, "Add EF Core context and mappings.", "Add EF Core context and mappings.", new DateOnly(2026, 3, 22), 10, "JIRA-2", 4, 1, 1, 1, 2, 3, 9, 1, "Implement SQL Server persistence model", 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, 1m, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 2, "Render issue columns in the desktop app.", "Render issue columns in the desktop app.", new DateOnly(2026, 3, 25), 16, "JIRA-3", 3, 1, 1, 1, 3, 8, 10, 6, "Build board screen", 3, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, 1m, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1, "Provide an initial admin credential for local login.", "Provide an initial admin credential for local login.", new DateOnly(2026, 3, 12), 2, "JIRA-4", 2, 1, 3, 1, 4, 1, 0, 2, "Seed admin login", 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "ActivityLogs",
                columns: new[] { "Id", "ActionType", "CreatedAtUtc", "FieldName", "IssueId", "MetadataJson", "NewValue", "OccurredAtUtc", "OldValue", "ProjectId", "UpdatedAtUtc", "UserId" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, null, "Set up WinForms solution skeleton", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 2, 4, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "Status", 3, null, "InProgress", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "Selected", 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 2 }
                });

            migrationBuilder.InsertData(
                table: "Comments",
                columns: new[] { "Id", "Body", "CreatedAtUtc", "IssueId", "UpdatedAtUtc", "UserId" },
                values: new object[,]
                {
                    { 1, "Board rendering is in progress.", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 3, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 3 },
                    { 2, "Schema should mirror the migration plan.", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 2, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1 }
                });

            migrationBuilder.InsertData(
                table: "IssueAssignees",
                columns: new[] { "IssueId", "UserId", "AssignedAtUtc" },
                values: new object[,]
                {
                    { 1, 2, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, 3, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, 2, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, 3, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_IssueId_OccurredAtUtc",
                table: "ActivityLogs",
                columns: new[] { "IssueId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_ProjectId_OccurredAtUtc",
                table: "ActivityLogs",
                columns: new[] { "ProjectId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_UserId",
                table: "ActivityLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_ChecksumSha256",
                table: "Attachments",
                column: "ChecksumSha256");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_IssueId_UploadedAtUtc",
                table: "Attachments",
                columns: new[] { "IssueId", "UploadedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_UploadedById",
                table: "Attachments",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_BoardColumns_ProjectId_StatusCode",
                table: "BoardColumns",
                columns: new[] { "ProjectId", "StatusCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Comments_IssueId_CreatedAtUtc",
                table: "Comments",
                columns: new[] { "IssueId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_UserId",
                table: "Comments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueAssignees_IssueId_AssignedAtUtc",
                table: "IssueAssignees",
                columns: new[] { "IssueId", "AssignedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueAssignees_UserId",
                table: "IssueAssignees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_CreatedById",
                table: "Issues",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_ProjectId_IssueKey",
                table: "Issues",
                columns: new[] { "ProjectId", "IssueKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Issues_ProjectId_SprintId_Status",
                table: "Issues",
                columns: new[] { "ProjectId", "SprintId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_ProjectId_Status_BoardPosition",
                table: "Issues",
                columns: new[] { "ProjectId", "Status", "BoardPosition" });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_ReporterId",
                table: "Issues",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_SprintId",
                table: "Issues",
                column: "SprintId");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_UpdatedAtUtc",
                table: "Issues",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_ProjectId_ProjectRole",
                table: "ProjectMembers",
                columns: new[] { "ProjectId", "ProjectRole" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_UserId",
                table: "ProjectMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Key",
                table: "Projects",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_ProjectId_StartDate",
                table: "Sprints",
                columns: new[] { "ProjectId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_ProjectId_State",
                table: "Sprints",
                columns: new[] { "ProjectId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLogs");

            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "BoardColumns");

            migrationBuilder.DropTable(
                name: "Comments");

            migrationBuilder.DropTable(
                name: "IssueAssignees");

            migrationBuilder.DropTable(
                name: "ProjectMembers");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Issues");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Sprints");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
