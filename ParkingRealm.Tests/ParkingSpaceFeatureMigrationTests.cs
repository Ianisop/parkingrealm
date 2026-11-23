using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class ParkingSpaceFeatureMigrationTests
{
    [Fact]
    public async Task Existing_inventory_is_classified_by_id_order_even_with_id_gaps()
    {
        var connection = new SqlConnectionStringBuilder(ApiFactory.TestConnectionString)
        {
            InitialCatalog = $"ParkingRealm_FeatureMigration_{Guid.NewGuid():N}"
        };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261109135510_AddBooking");
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO [ParkingSpaces] ([Row], [Column])
                VALUES (0,0), (0,1), (0,2), (0,3), (0,4), (0,5), (0,6), (0,7), (0,8);
                DELETE FROM [ParkingSpaces] WHERE [Column] = 2;
                """);
            await migrator.MigrateAsync();
            var spaces = await db.ParkingSpaces.AsNoTracking().OrderBy(space => space.Id).ToListAsync();
            Assert.Equal(8, spaces.Count);
            Assert.Equal(new[] { 0, 1, 3, 4, 5, 6, 7, 8 }, spaces.Select(space => space.Column));
            Assert.Equal(spaces.Take(2).Select(space => space.Id), spaces.Where(space => space.IsAccessible).Select(space => space.Id));
            Assert.Equal(spaces.TakeLast(4).Select(space => space.Id), spaces.Where(space => space.HasCharger).Select(space => space.Id));
            Assert.False(db.Database.HasPendingModelChanges());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
