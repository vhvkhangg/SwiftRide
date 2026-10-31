using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftRide.TripService.Migrations
{
    /// <inheritdoc />
    public partial class TripIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Trips",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayloadHash",
                table: "Trips",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_IdempotencyKey",
                table: "Trips",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trips_IdempotencyKey",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PayloadHash",
                table: "Trips");
        }
    }
}
