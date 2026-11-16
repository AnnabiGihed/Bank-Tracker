using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KbcTracker.Persistence.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class Debts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Debts",
                table: "UserData",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Debts",
                table: "UserData");
        }
    }
}
