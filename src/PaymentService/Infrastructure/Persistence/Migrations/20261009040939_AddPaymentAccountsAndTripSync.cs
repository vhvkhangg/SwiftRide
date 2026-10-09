using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftRide.PaymentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentAccountsAndTripSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_trip_synced",
                table: "payments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "payee_account",
                table: "payments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "payer_account",
                table: "payments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_trip_synced",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "payee_account",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "payer_account",
                table: "payments");
        }
    }
}
