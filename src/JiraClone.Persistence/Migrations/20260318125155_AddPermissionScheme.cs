using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace JiraClone.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionScheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PermissionSchemes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionSchemes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissionSchemes_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermissionGrants",
                columns: table => new
                {
                    PermissionSchemeId = table.Column<int>(type: "int", nullable: false),
                    Permission = table.Column<int>(type: "int", nullable: false),
                    ProjectRole = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionGrants", x => new { x.PermissionSchemeId, x.Permission, x.ProjectRole });
                    table.ForeignKey(
                        name: "FK_PermissionGrants_PermissionSchemes_PermissionSchemeId",
                        column: x => x.PermissionSchemeId,
                        principalTable: "PermissionSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "PermissionSchemes",
                columns: new[] { "Id", "CreatedAtUtc", "Name", "ProjectId", "UpdatedAtUtc" },
                values: new object[] { 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc), "Default Permission Scheme", 1, new DateTime(2026, 3, 10, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "PermissionGrants",
                columns: new[] { "Permission", "PermissionSchemeId", "ProjectRole" },
                values: new object[,]
                {
                    { 1, 1, 2 },
                    { 1, 1, 3 },
                    { 1, 1, 4 },
                    { 2, 1, 2 },
                    { 2, 1, 3 },
                    { 2, 1, 4 },
                    { 3, 1, 2 },
                    { 3, 1, 3 },
                    { 3, 1, 4 },
                    { 4, 1, 2 },
                    { 4, 1, 3 },
                    { 4, 1, 4 },
                    { 5, 1, 3 },
                    { 5, 1, 4 },
                    { 6, 1, 3 },
                    { 6, 1, 4 },
                    { 7, 1, 3 },
                    { 7, 1, 4 },
                    { 8, 1, 3 },
                    { 8, 1, 4 },
                    { 9, 1, 1 },
                    { 9, 1, 2 },
                    { 9, 1, 3 },
                    { 9, 1, 4 },
                    { 10, 1, 2 },
                    { 10, 1, 3 },
                    { 10, 1, 4 },
                    { 11, 1, 2 },
                    { 11, 1, 3 },
                    { 11, 1, 4 },
                    { 12, 1, 2 },
                    { 12, 1, 3 },
                    { 12, 1, 4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionSchemes_ProjectId",
                table: "PermissionSchemes",
                column: "ProjectId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermissionGrants");

            migrationBuilder.DropTable(
                name: "PermissionSchemes");
        }
    }
}
