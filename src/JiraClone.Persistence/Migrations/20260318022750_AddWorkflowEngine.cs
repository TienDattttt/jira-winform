using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace JiraClone.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Issues",
                newName: "WorkflowStatusId");

            migrationBuilder.RenameIndex(
                name: "IX_Issues_ProjectId_Status_BoardPosition",
                table: "Issues",
                newName: "IX_Issues_ProjectId_WorkflowStatusId_BoardPosition");

            migrationBuilder.RenameIndex(
                name: "IX_Issues_ProjectId_SprintId_Status",
                table: "Issues",
                newName: "IX_Issues_ProjectId_SprintId_WorkflowStatusId");

            migrationBuilder.RenameColumn(
                name: "StatusCode",
                table: "BoardColumns",
                newName: "WorkflowStatusId");

            migrationBuilder.RenameIndex(
                name: "IX_BoardColumns_ProjectId_StatusCode",
                table: "BoardColumns",
                newName: "IX_BoardColumns_ProjectId_WorkflowStatusId");

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowDefinitions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowStatuses_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionId = table.Column<int>(type: "int", nullable: false),
                    FromStatusId = table.Column<int>(type: "int", nullable: false),
                    ToStatusId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_WorkflowStatuses_FromStatusId",
                        column: x => x.FromStatusId,
                        principalTable: "WorkflowStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_WorkflowStatuses_ToStatusId",
                        column: x => x.ToStatusId,
                        principalTable: "WorkflowStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransitionRoles",
                columns: table => new
                {
                    WorkflowTransitionId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransitionRoles", x => new { x.WorkflowTransitionId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_WorkflowTransitionRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitionRoles_WorkflowTransitions_WorkflowTransitionId",
                        column: x => x.WorkflowTransitionId,
                        principalTable: "WorkflowTransitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "ActivityLogs",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "FieldName", "MetadataJson", "NewValue" },
                values: new object[] { "WorkflowStatusId", "{\"OldStatusId\":2,\"OldStatusName\":\"Selected\",\"OldCategory\":1,\"NewStatusId\":3,\"NewStatusName\":\"In Progress\",\"NewCategory\":2}", "In Progress" });

            migrationBuilder.InsertData(
                table: "WorkflowDefinitions",
                columns: new[] { "Id", "CreatedAtUtc", "IsDefault", "Name", "ProjectId", "UpdatedAtUtc" },
                values: new object[] { 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), true, "Default Workflow", 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "WorkflowStatuses",
                columns: new[] { "Id", "Category", "Color", "CreatedAtUtc", "DisplayOrder", "Name", "UpdatedAtUtc", "WorkflowDefinitionId" },
                values: new object[,]
                {
                    { 1, 1, "#42526E", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1, "Backlog", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 2, 1, "#4C9AFF", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 2, "Selected", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 3, 2, "#0052CC", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 3, "In Progress", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 4, 3, "#36B37E", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 4, "Done", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1 }
                });

            migrationBuilder.InsertData(
                table: "WorkflowTransitions",
                columns: new[] { "Id", "CreatedAtUtc", "FromStatusId", "Name", "ToStatusId", "UpdatedAtUtc", "WorkflowDefinitionId" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1, "Backlog to Selected", 2, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 2, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1, "Backlog to In Progress", 3, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 3, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1, "Backlog to Done", 4, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 4, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 2, "Selected to Backlog", 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 5, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 2, "Selected to In Progress", 3, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 6, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 2, "Selected to Done", 4, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 7, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 3, "In Progress to Backlog", 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 8, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 3, "In Progress to Selected", 2, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 9, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 3, "In Progress to Done", 4, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 10, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 4, "Done to Backlog", 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 11, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 4, "Done to Selected", 2, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 12, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 4, "Done to In Progress", 3, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), 1 }
                });

            migrationBuilder.InsertData(
                table: "WorkflowTransitionRoles",
                columns: new[] { "RoleId", "WorkflowTransitionId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 1 },
                    { 3, 1 },
                    { 1, 2 },
                    { 2, 2 },
                    { 3, 2 },
                    { 1, 3 },
                    { 2, 3 },
                    { 3, 3 },
                    { 1, 4 },
                    { 2, 4 },
                    { 3, 4 },
                    { 1, 5 },
                    { 2, 5 },
                    { 3, 5 },
                    { 1, 6 },
                    { 2, 6 },
                    { 3, 6 },
                    { 1, 7 },
                    { 2, 7 },
                    { 3, 7 },
                    { 1, 8 },
                    { 2, 8 },
                    { 3, 8 },
                    { 1, 9 },
                    { 2, 9 },
                    { 3, 9 },
                    { 1, 10 },
                    { 2, 10 },
                    { 3, 10 },
                    { 1, 11 },
                    { 2, 11 },
                    { 3, 11 },
                    { 1, 12 },
                    { 2, 12 },
                    { 3, 12 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_WorkflowStatusId",
                table: "Issues",
                column: "WorkflowStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardColumns_ProjectId_DisplayOrder",
                table: "BoardColumns",
                columns: new[] { "ProjectId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoardColumns_WorkflowStatusId",
                table: "BoardColumns",
                column: "WorkflowStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_ProjectId_IsDefault",
                table: "WorkflowDefinitions",
                columns: new[] { "ProjectId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_ProjectId_Name",
                table: "WorkflowDefinitions",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStatuses_WorkflowDefinitionId_DisplayOrder",
                table: "WorkflowStatuses",
                columns: new[] { "WorkflowDefinitionId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStatuses_WorkflowDefinitionId_Name",
                table: "WorkflowStatuses",
                columns: new[] { "WorkflowDefinitionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionRoles_RoleId",
                table: "WorkflowTransitionRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_FromStatusId",
                table: "WorkflowTransitions",
                column: "FromStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_ToStatusId",
                table: "WorkflowTransitions",
                column: "ToStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_WorkflowDefinitionId_FromStatusId_ToStatusId",
                table: "WorkflowTransitions",
                columns: new[] { "WorkflowDefinitionId", "FromStatusId", "ToStatusId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardColumns_WorkflowStatuses_WorkflowStatusId",
                table: "BoardColumns",
                column: "WorkflowStatusId",
                principalTable: "WorkflowStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_WorkflowStatuses_WorkflowStatusId",
                table: "Issues",
                column: "WorkflowStatusId",
                principalTable: "WorkflowStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BoardColumns_WorkflowStatuses_WorkflowStatusId",
                table: "BoardColumns");

            migrationBuilder.DropForeignKey(
                name: "FK_Issues_WorkflowStatuses_WorkflowStatusId",
                table: "Issues");

            migrationBuilder.DropTable(
                name: "WorkflowTransitionRoles");

            migrationBuilder.DropTable(
                name: "WorkflowTransitions");

            migrationBuilder.DropTable(
                name: "WorkflowStatuses");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Issues_WorkflowStatusId",
                table: "Issues");

            migrationBuilder.DropIndex(
                name: "IX_BoardColumns_ProjectId_DisplayOrder",
                table: "BoardColumns");

            migrationBuilder.DropIndex(
                name: "IX_BoardColumns_WorkflowStatusId",
                table: "BoardColumns");

            migrationBuilder.RenameColumn(
                name: "WorkflowStatusId",
                table: "Issues",
                newName: "Status");

            migrationBuilder.RenameIndex(
                name: "IX_Issues_ProjectId_WorkflowStatusId_BoardPosition",
                table: "Issues",
                newName: "IX_Issues_ProjectId_Status_BoardPosition");

            migrationBuilder.RenameIndex(
                name: "IX_Issues_ProjectId_SprintId_WorkflowStatusId",
                table: "Issues",
                newName: "IX_Issues_ProjectId_SprintId_Status");

            migrationBuilder.RenameColumn(
                name: "WorkflowStatusId",
                table: "BoardColumns",
                newName: "StatusCode");

            migrationBuilder.RenameIndex(
                name: "IX_BoardColumns_ProjectId_WorkflowStatusId",
                table: "BoardColumns",
                newName: "IX_BoardColumns_ProjectId_StatusCode");

            migrationBuilder.UpdateData(
                table: "ActivityLogs",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "FieldName", "MetadataJson", "NewValue" },
                values: new object[] { "Status", null, "InProgress" });
        }
    }
}
