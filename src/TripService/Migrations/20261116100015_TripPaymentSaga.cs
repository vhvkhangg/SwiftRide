using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftRide.TripService.Migrations
{
    /// <inheritdoc />
    public partial class TripPaymentSaga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TripPaymentSagas",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PayerAccount = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayeeAccount = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommandsSent = table.Column<int>(type: "integer", nullable: false),
                    FaultCount = table.Column<int>(type: "integer", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeadlineUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TripAlreadyPaid = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripPaymentSagas", x => x.CorrelationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TripPaymentSagas_CurrentState_DeadlineUtc",
                table: "TripPaymentSagas",
                columns: new[] { "CurrentState", "DeadlineUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TripPaymentSagas");
        }
    }
}
