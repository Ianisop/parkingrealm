using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class VehicleElectricMigrationTests
{
    [Fact]
    public async Task Existing_vehicles_default_to_non_electric_and_keep_their_reservations()
    {
        var connection = new SqlConnectionStringBuilder(ApiFactory.TestConnectionString)
        {
            InitialCatalog = $"ParkingRealm_VehicleMigration_{Guid.NewGuid():N}"
        };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261116074036_AddParkingSpaceFeatures");
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO [AspNetUsers]
                    ([Id], [FirstName], [LastName], [EmailConfirmed], [PhoneNumberConfirmed],
                     [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount])
                VALUES ('migration-owner', 'Test', 'Customer', 0, 0, 0, 0, 0);
                INSERT INTO [Vehicles] ([OwnerId], [RegistrationNumber], [NormalizedRegistrationNumber])
                VALUES ('migration-owner', 'OLD123', 'OLD123');
                DECLARE @VehicleId int = SCOPE_IDENTITY();
                INSERT INTO [ParkingSpaces] ([Row], [Column], [IsAccessible], [HasCharger]) VALUES (0, 0, 0, 0);
                DECLARE @SpaceId int = SCOPE_IDENTITY();
                INSERT INTO [Reservations] ([OwnerId], [VehicleId], [ParkingSpaceId], [Start], [End], [Status], [CreatedAt])
                VALUES ('migration-owner', @VehicleId, @SpaceId, '2090-01-01T10:00:00Z', '2090-01-01T11:00:00Z', 'Active', SYSDATETIMEOFFSET());
                """);
            await migrator.MigrateAsync();
            var vehicle = Assert.Single(await db.Vehicles.AsNoTracking().ToListAsync());
            Assert.False(vehicle.IsElectric);
            Assert.Equal("OLD123", vehicle.RegistrationNumber);
            var reservation = Assert.Single(await db.Reservations.AsNoTracking().ToListAsync());
            Assert.Equal(vehicle.Id, reservation.VehicleId);
            Assert.Equal(ReservationStatus.Active, reservation.Status);
            Assert.False(db.Database.HasPendingModelChanges());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
