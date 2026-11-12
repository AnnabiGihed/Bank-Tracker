using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KbcTracker.Persistence.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class PaymentSplits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TxnSplits",
                table: "UserData",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TxnSplits",
                table: "UserData");
        }
    }
}
