using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingRealm.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParkingSpaceFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasCharger",
                table: "ParkingSpaces",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAccessible",
                table: "ParkingSpaces",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                WITH RankedSpaces AS (
                    SELECT [Id],
                        ROW_NUMBER() OVER (ORDER BY [Id]) AS [FirstPosition],
                        ROW_NUMBER() OVER (ORDER BY [Id] DESC) AS [LastPosition]
                    FROM [ParkingSpaces]
                )
                UPDATE space
                SET [IsAccessible] = CASE WHEN ranked.[FirstPosition] <= 2 THEN 1 ELSE 0 END,
                    [HasCharger] = CASE WHEN ranked.[LastPosition] <= 4 THEN 1 ELSE 0 END
                FROM [ParkingSpaces] space
                INNER JOIN RankedSpaces ranked ON ranked.[Id] = space.[Id];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasCharger",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "IsAccessible",
                table: "ParkingSpaces");
        }
    }
}
