using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftRide.PaymentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                table: "payments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Wallet");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "payments");
        }
    }
}
