using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingRealm.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParkingSpaces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Row = table.Column<int>(type: "int", nullable: false),
                    Column = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingSpaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NormalizedRegistrationNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.UniqueConstraint("AK_Vehicles_Id_OwnerId", x => new { x.Id, x.OwnerId });
                    table.ForeignKey(
                        name: "FK_Vehicles_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    ParkingSpaceId = table.Column<int>(type: "int", nullable: false),
                    Start = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    End = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.CheckConstraint("CK_Reservations_Interval", "[End] > [Start]");
                    table.ForeignKey(
                        name: "FK_Reservations_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_ParkingSpaces_ParkingSpaceId",
                        column: x => x.ParkingSpaceId,
                        principalTable: "ParkingSpaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_Vehicles_VehicleId_OwnerId",
                        columns: x => new { x.VehicleId, x.OwnerId },
                        principalTable: "Vehicles",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSpaces_Row_Column",
                table: "ParkingSpaces",
                columns: new[] { "Row", "Column" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_OwnerId_Start",
                table: "Reservations",
                columns: new[] { "OwnerId", "Start" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ParkingSpaceId_Status_Start_End",
                table: "Reservations",
                columns: new[] { "ParkingSpaceId", "Status", "Start", "End" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_VehicleId_OwnerId",
                table: "Reservations",
                columns: new[] { "VehicleId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_OwnerId_NormalizedRegistrationNumber",
                table: "Vehicles",
                columns: new[] { "OwnerId", "NormalizedRegistrationNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reservations");

            migrationBuilder.DropTable(
                name: "ParkingSpaces");

            migrationBuilder.DropTable(
                name: "Vehicles");
        }
    }
}
