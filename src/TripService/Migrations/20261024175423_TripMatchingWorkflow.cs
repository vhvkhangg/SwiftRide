using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftRide.TripService.Migrations
{
    /// <inheritdoc />
    public partial class TripMatchingWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MatchingAttempt",
                table: "Trips",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PaymentFailureReason",
                table: "Trips",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PickupLatitude",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PickupLongitude",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Attempt",
                table: "DriverInvitations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsResolved",
                table: "DriverInvitations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_DriverInvitations_TripId_Attempt",
                table: "DriverInvitations",
                columns: new[] { "TripId", "Attempt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DriverInvitations_TripId_Attempt",
                table: "DriverInvitations");

            migrationBuilder.DropColumn(
                name: "MatchingAttempt",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PaymentFailureReason",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PickupLatitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PickupLongitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "Attempt",
                table: "DriverInvitations");

            migrationBuilder.DropColumn(
                name: "IsResolved",
                table: "DriverInvitations");
        }
    }
}
