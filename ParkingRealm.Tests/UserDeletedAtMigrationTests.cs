using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class UserDeletedAtMigrationTests
{
    [Fact]
    public async Task Existing_users_are_not_deleted_after_migration()
    {
        var connection = new SqlConnectionStringBuilder(ApiFactory.TestConnectionString)
        {
            InitialCatalog = $"ParkingRealm_UserDeletedAt_{Guid.NewGuid():N}"
        };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261116075339_AddVehicleElectricClassification");
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO [AspNetUsers]
                    ([Id], [FirstName], [LastName], [EmailConfirmed], [PhoneNumberConfirmed],
                     [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount])
                VALUES ('migration-user', 'Test', 'Customer', 0, 0, 0, 0, 0);
                """);
            await migrator.MigrateAsync();
            var user = Assert.Single(await db.Users.AsNoTracking().ToListAsync());
            Assert.Null(user.DeletedAt);
            Assert.False(db.Database.HasPendingModelChanges());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
