using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JiraClone.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintSoftDeleteForProjectDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sprints_ProjectId",
                table: "Sprints");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Sprints",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_ProjectId",
                table: "Sprints",
                column: "ProjectId",
                unique: true,
                filter: "[State] = 2 AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sprints_ProjectId",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Sprints");

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_ProjectId",
                table: "Sprints",
                column: "ProjectId",
                unique: true,
                filter: "[State] = 2");
        }
    }
}