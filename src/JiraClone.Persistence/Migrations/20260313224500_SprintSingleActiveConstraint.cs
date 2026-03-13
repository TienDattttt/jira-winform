using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JiraClone.Persistence.Migrations;

public partial class SprintSingleActiveConstraint : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Sprints_ProjectId_ActiveUnique",
            table: "Sprints",
            column: "ProjectId",
            unique: true,
            filter: "[State] = 2");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Sprints_ProjectId_ActiveUnique",
            table: "Sprints");
    }
}
