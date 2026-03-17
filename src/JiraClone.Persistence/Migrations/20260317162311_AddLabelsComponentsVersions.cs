using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace JiraClone.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLabelsComponentsVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FixVersionId",
                table: "Issues",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Components",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    LeadUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Components", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Components_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Components_Users_LeadUserId",
                        column: x => x.LeadUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Labels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Labels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Labels_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    ReleaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsReleased = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectVersions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IssueComponents",
                columns: table => new
                {
                    IssueId = table.Column<int>(type: "int", nullable: false),
                    ComponentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueComponents", x => new { x.IssueId, x.ComponentId });
                    table.ForeignKey(
                        name: "FK_IssueComponents_Components_ComponentId",
                        column: x => x.ComponentId,
                        principalTable: "Components",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssueComponents_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IssueLabels",
                columns: table => new
                {
                    IssueId = table.Column<int>(type: "int", nullable: false),
                    LabelId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueLabels", x => new { x.IssueId, x.LabelId });
                    table.ForeignKey(
                        name: "FK_IssueLabels_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssueLabels_Labels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "Labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Components",
                columns: new[] { "Id", "CreatedAtUtc", "Description", "LeadUserId", "Name", "ProjectId", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "Column layout, issue cards, and interaction polish.", 2, "Board UI", 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "Entity Framework mappings and repository behavior.", 3, "Persistence", 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.UpdateData(
                table: "Issues",
                keyColumn: "Id",
                keyValue: 1,
                column: "FixVersionId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Issues",
                keyColumn: "Id",
                keyValue: 2,
                column: "FixVersionId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Issues",
                keyColumn: "Id",
                keyValue: 3,
                column: "FixVersionId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Issues",
                keyColumn: "Id",
                keyValue: 4,
                column: "FixVersionId",
                value: 2);

            migrationBuilder.InsertData(
                table: "Labels",
                columns: new[] { "Id", "Color", "CreatedAtUtc", "Name", "ProjectId", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 1, "#4688EC", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "Desktop", 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "#2ABB7F", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "Migration", 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "#FCA700", new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "Board", 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "ProjectVersions",
                columns: new[] { "Id", "CreatedAtUtc", "Description", "IsReleased", "Name", "ProjectId", "ReleaseDate", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "First usable internal desktop milestone.", false, "Desktop MVP", 1, new DateTime(2026, 3, 31, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "Initial feature-complete alpha cut.", true, "Alpha Cut", 1, new DateTime(2026, 3, 20, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "IssueComponents",
                columns: new[] { "ComponentId", "IssueId" },
                values: new object[,]
                {
                    { 2, 2 },
                    { 1, 3 }
                });

            migrationBuilder.InsertData(
                table: "IssueLabels",
                columns: new[] { "IssueId", "LabelId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 1, 2 },
                    { 2, 2 },
                    { 3, 1 },
                    { 3, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_FixVersionId",
                table: "Issues",
                column: "FixVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Components_LeadUserId",
                table: "Components",
                column: "LeadUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Components_ProjectId_Name",
                table: "Components",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IssueComponents_ComponentId",
                table: "IssueComponents",
                column: "ComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueLabels_LabelId",
                table: "IssueLabels",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "IX_Labels_ProjectId_Name",
                table: "Labels",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersions_ProjectId_IsReleased",
                table: "ProjectVersions",
                columns: new[] { "ProjectId", "IsReleased" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersions_ProjectId_Name",
                table: "ProjectVersions",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_ProjectVersions_FixVersionId",
                table: "Issues",
                column: "FixVersionId",
                principalTable: "ProjectVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Issues_ProjectVersions_FixVersionId",
                table: "Issues");

            migrationBuilder.DropTable(
                name: "IssueComponents");

            migrationBuilder.DropTable(
                name: "IssueLabels");

            migrationBuilder.DropTable(
                name: "ProjectVersions");

            migrationBuilder.DropTable(
                name: "Components");

            migrationBuilder.DropTable(
                name: "Labels");

            migrationBuilder.DropIndex(
                name: "IX_Issues_FixVersionId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "FixVersionId",
                table: "Issues");
        }
    }
}
