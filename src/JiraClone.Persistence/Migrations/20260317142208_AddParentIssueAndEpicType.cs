using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JiraClone.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParentIssueAndEpicType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentIssueId",
                table: "Issues",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Issues",
                keyColumn: "Id",
                keyValue: 1,
                column: "ParentIssueId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Issues",
                keyColumn: "Id",
                keyValue: 2,
                column: "ParentIssueId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Issues",
                keyColumn: "Id",
                keyValue: 3,
                column: "ParentIssueId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Issues",
                keyColumn: "Id",
                keyValue: 4,
                column: "ParentIssueId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_ProjectId",
                table: "Sprints",
                column: "ProjectId",
                unique: true,
                filter: "[State] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_ParentIssueId",
                table: "Issues",
                column: "ParentIssueId");

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_Issues_ParentIssueId",
                table: "Issues",
                column: "ParentIssueId",
                principalTable: "Issues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Issues_Issues_ParentIssueId",
                table: "Issues");

            migrationBuilder.DropIndex(
                name: "IX_Sprints_ProjectId",
                table: "Sprints");

            migrationBuilder.DropIndex(
                name: "IX_Issues_ParentIssueId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "ParentIssueId",
                table: "Issues");
        }
    }
}
