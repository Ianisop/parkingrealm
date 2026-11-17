using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class DevelopmentParkingInventoryTests
{
    [Fact]
    public async Task Empty_development_database_gets_two_accessible_and_four_charging_spaces()
    {
        var connection = new SqlConnectionStringBuilder(ApiFactory.TestConnectionString)
        {
            InitialCatalog = $"ParkingRealm_DevelopmentInventory_{Guid.NewGuid():N}"
        };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        try
        {
            await using var app = new ApiFactory(connection.ConnectionString);
            using var client = app.CreateClient();
            var response = await client.GetFromJsonAsync<JsonElement[]>("/api/parking-spaces");
            Assert.NotNull(response);
            var spaces = response.OrderBy(space => space.GetProperty("id").GetInt32()).ToArray();
            Assert.Equal(20, spaces.Length);
            for (var index = 0; index < spaces.Length; index++)
            {
                Assert.Equal(index < 2, spaces[index].GetProperty("isAccessible").GetBoolean());
                Assert.Equal(index >= 16, spaces[index].GetProperty("hasCharger").GetBoolean());
            }
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
