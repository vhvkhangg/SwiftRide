using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftRide.PaymentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentVersionAndUniqueSettledTrip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payments_trip_id",
                table: "payments");

            migrationBuilder.AddColumn<long>(
                name: "version",
                table: "payments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "ux_payments_settled_trip",
                table: "payments",
                column: "trip_id",
                unique: true,
                filter: "status IN ('Succeeded', 'Refunded')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_payments_settled_trip",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "version",
                table: "payments");

            migrationBuilder.CreateIndex(
                name: "ix_payments_trip_id",
                table: "payments",
                column: "trip_id");
        }
    }
}
