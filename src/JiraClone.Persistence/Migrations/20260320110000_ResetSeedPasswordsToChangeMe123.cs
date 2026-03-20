using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JiraClone.Persistence.Migrations
{
    public partial class ResetSeedPasswordsToChangeMe123 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "PasswordHash", "PasswordSalt" },
                values: new object[] { "jgpZxNxCoXwMhOTNRy7GXZHyX6pwZyruG7q31ducr54=", "hyp1jJnol7RJsq08AjbBaw==" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "PasswordHash", "PasswordSalt" },
                values: new object[] { "jgpZxNxCoXwMhOTNRy7GXZHyX6pwZyruG7q31ducr54=", "hyp1jJnol7RJsq08AjbBaw==" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "PasswordHash", "PasswordSalt" },
                values: new object[] { "jgpZxNxCoXwMhOTNRy7GXZHyX6pwZyruG7q31ducr54=", "hyp1jJnol7RJsq08AjbBaw==" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "PasswordHash", "PasswordSalt" },
                values: new object[] { "kgu+8rnxTM1wAdC5cmxrD58zMWHiBN9ocgia5jcEKlU=", "E8M6vZg6o0mM1k3m8Xx3MQ==" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "PasswordHash", "PasswordSalt" },
                values: new object[] { "kgu+8rnxTM1wAdC5cmxrD58zMWHiBN9ocgia5jcEKlU=", "E8M6vZg6o0mM1k3m8Xx3MQ==" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "PasswordHash", "PasswordSalt" },
                values: new object[] { "kgu+8rnxTM1wAdC5cmxrD58zMWHiBN9ocgia5jcEKlU=", "E8M6vZg6o0mM1k3m8Xx3MQ==" });
        }
    }
}
