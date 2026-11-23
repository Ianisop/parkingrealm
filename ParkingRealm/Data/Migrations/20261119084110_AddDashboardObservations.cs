using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingRealm.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceObservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReservationId = table.Column<int>(type: "int", nullable: false),
                    Attended = table.Column<bool>(type: "bit", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceObservations_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OccupancyObservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Occupied = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OccupancyObservations", x => x.Id);
                    table.CheckConstraint("CK_Occupancy_Counts", "[Capacity] > 0 AND [Occupied] >= 0 AND [Occupied] <= [Capacity]");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceObservations_ReservationId",
                table: "AttendanceObservations",
                column: "ReservationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OccupancyObservations_RecordedAt",
                table: "OccupancyObservations",
                column: "RecordedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceObservations");

            migrationBuilder.DropTable(
                name: "OccupancyObservations");
        }
    }
}
