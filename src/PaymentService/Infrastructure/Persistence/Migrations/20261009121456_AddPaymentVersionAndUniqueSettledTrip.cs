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
            // Fail with an actionable message before creating the unique index.
            // Do not silently discard payment/ledger records during migration.
            migrationBuilder.Sql("""
                DO $$
                DECLARE duplicate_trip_count integer;
                BEGIN
                    SELECT COUNT(*) INTO duplicate_trip_count
                    FROM (
                        SELECT trip_id
                        FROM payments
                        WHERE status IN ('Succeeded', 'Refunded')
                        GROUP BY trip_id
                        HAVING COUNT(*) > 1
                    ) AS duplicate_trips;

                    IF duplicate_trip_count > 0 THEN
                        RAISE EXCEPTION
                            'Cannot add ux_payments_settled_trip: % trip(s) have multiple settled payments',
                            duplicate_trip_count
                            USING HINT =
                                'Inspect payments grouped by trip_id with status Succeeded/Refunded. ' ||
                                'Reconcile each duplicate and its ledger entries manually, or reset only disposable ' ||
                                'development data, then rerun the migration.';
                    END IF;
                END $$;
                """);

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
