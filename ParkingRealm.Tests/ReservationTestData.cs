using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

internal sealed class ReservationTestData(ApiFactory factory, HttpClient client, string ownerId, int vehicleId, int spaceId) : IAsyncDisposable
{
    public HttpClient Client { get; } = client;
    public string OwnerId { get; } = ownerId;
    public int VehicleId { get; } = vehicleId;
    public int SpaceId { get; } = spaceId;
    public DateTimeOffset Start { get; } = DateTimeOffset.UtcNow.AddDays(1);

    public static async Task<ReservationTestData> CreateAsync(ApiFactory factory)
    {
        var client = await BookingCustomer.LoginAsync(factory);
        var user = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var response = await client.PostAsJsonAsync("/api/vehicles", new { registrationNumber = "AB12345" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var vehicle = await response.Content.ReadFromJsonAsync<JsonElement>();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var space = new ParkingSpace { Row = Random.Shared.Next(1, 1000000), Column = 0 };
        db.ParkingSpaces.Add(space);
        await db.SaveChangesAsync();
        return new(factory, client, user.GetProperty("id").GetString()!, vehicle.GetProperty("id").GetInt32(), space.Id);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Reservations.Where(r => r.ParkingSpaceId == SpaceId || r.OwnerId == OwnerId).ExecuteDeleteAsync();
        await db.ParkingSpaces.Where(s => s.Id == SpaceId).ExecuteDeleteAsync();
        await db.Vehicles.Where(v => v.OwnerId == OwnerId).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id == OwnerId).ExecuteDeleteAsync();
    }
}
