using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class InventoryProvisioningTests(ApiFactory factory)
{
    private static async Task ProvisionAsync(ApplicationDbContext db, string inventory)
    {
        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "ParkingSpaces.sql"));
        await db.Database.ExecuteSqlRawAsync(sql, new SqlParameter("@Inventory", inventory));
    }

    [Fact]
    public async Task Empty_inventory_has_no_spaces()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        await ProvisionAsync(db, "[]");

        Assert.Empty(await db.ParkingSpaces.ToListAsync());
    }

    [Fact]
    public async Task Provisioning_preserves_ids_when_repeated()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        const string inventory = "[{\"row\":0,\"column\":0},{\"row\":0,\"column\":1}]";

        await ProvisionAsync(db, inventory);
        var originalIds = await db.ParkingSpaces.OrderBy(s => s.Column).Select(s => s.Id).ToListAsync();
        await ProvisionAsync(db, inventory);
        var repeatedIds = await db.ParkingSpaces.OrderBy(s => s.Column).Select(s => s.Id).ToListAsync();

        Assert.Equal(2, originalIds.Count);
        Assert.Equal(originalIds, repeatedIds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(10)]
    public async Task Provisioning_designates_first_two_accessible_and_last_four_charging_spaces(int count)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var inventory = System.Text.Json.JsonSerializer.Serialize(Enumerable.Range(0, count).Reverse()
            .Select(column => new { row = 0, column }));
        await ProvisionAsync(db, inventory);
        var spaces = await db.ParkingSpaces.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
        Assert.Equal(count, spaces.Count);
        for (var index = 0; index < count; index++)
        {
            Assert.Equal(index, spaces[index].Column);
            Assert.Equal(index < 2, spaces[index].IsAccessible);
            Assert.Equal(index >= count - 4, spaces[index].HasCharger);
        }
        await ProvisionAsync(db, inventory);
        var repeated = await db.ParkingSpaces.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
        Assert.Equal(spaces.Select(s => (s.Id, s.IsAccessible, s.HasCharger)),
            repeated.Select(s => (s.Id, s.IsAccessible, s.HasCharger)));
    }

    [Fact]
    public async Task Duplicate_coordinates_are_rejected_without_writing()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var error = await Assert.ThrowsAsync<SqlException>(() => ProvisionAsync(db,
            "[{\"row\":2,\"column\":1},{\"row\":2,\"column\":1}]"));

        Assert.Equal(50003, error.Number);
        Assert.Empty(await db.ParkingSpaces.ToListAsync());
    }
}
