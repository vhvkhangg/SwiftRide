using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftRide.TripService.Migrations
{
    /// <inheritdoc />
    public partial class Task2TripInvitationUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DriverInvitations_TripId_Attempt",
                table: "DriverInvitations");

            migrationBuilder.CreateIndex(
                name: "IX_DriverInvitations_TripId_Attempt",
                table: "DriverInvitations",
                columns: new[] { "TripId", "Attempt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DriverInvitations_TripId_Attempt",
                table: "DriverInvitations");

            migrationBuilder.CreateIndex(
                name: "IX_DriverInvitations_TripId_Attempt",
                table: "DriverInvitations",
                columns: new[] { "TripId", "Attempt" });
        }
    }
}
