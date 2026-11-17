using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingRealm.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleElectricClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsElectric",
                table: "Vehicles",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsElectric",
                table: "Vehicles");
        }
    }
}
