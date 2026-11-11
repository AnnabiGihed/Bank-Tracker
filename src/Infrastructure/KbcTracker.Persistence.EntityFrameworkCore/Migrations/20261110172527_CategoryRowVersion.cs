using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KbcTracker.Persistence.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class CategoryRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Categories",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Categories");
        }
    }
}
