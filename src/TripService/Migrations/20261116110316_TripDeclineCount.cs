using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftRide.TripService.Migrations
{
    /// <inheritdoc />
    public partial class TripDeclineCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeclineCount",
                table: "Trips",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeclineCount",
                table: "Trips");
        }
    }
}
