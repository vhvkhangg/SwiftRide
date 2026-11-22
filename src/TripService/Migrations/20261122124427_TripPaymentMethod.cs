using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftRide.TripService.Migrations
{
    /// <inheritdoc />
    public partial class TripPaymentMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Trips",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Wallet");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Trips");
        }
    }
}
