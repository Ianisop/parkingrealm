using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class ParkingEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Empty_inventory_returns_no_spaces()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/parking-spaces");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Empty((await response.Content.ReadFromJsonAsync<JsonElement[]>())!);
    }

    [Theory]
    [InlineData("?start=bad&end=bad")]
    [InlineData("?start=2030-01-01T10:00:00Z")]
    [InlineData("?start=2030-01-01T10:00:00Z&end=2030-01-01T10:00:00Z")]
    public async Task Invalid_interval_returns_validation_error(string query)
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/parking-spaces" + query)).StatusCode);
    }

    [Fact]
    public async Task Active_overlap_blocks_and_cancelled_reservation_does_not_block()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new ApplicationUser { FirstName = "Test", LastName = "Customer" };
        var vehicle = new Vehicle { Owner = owner, OwnerId = owner.Id, RegistrationNumber = "TEST", NormalizedRegistrationNumber = "TEST" };
        var space = new ParkingSpace { Row = 0, Column = 0 };
        var now = DateTimeOffset.UtcNow;
        var reservation = new Reservation
        {
            Owner = owner, OwnerId = owner.Id, Vehicle = vehicle, ParkingSpace = space,
            Start = now.AddHours(-1), End = now.AddHours(1), CreatedAt = now
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();
        try
        {
            using var client = factory.CreateClient();
            var current = await client.GetFromJsonAsync<JsonElement[]>("/api/parking-spaces");
            var item = Assert.Single(current!);
            Assert.False(item.GetProperty("isAvailable").GetBoolean());
            Assert.Equal(0, item.GetProperty("row").GetInt32());

            var query = $"?start={Uri.EscapeDataString(now.ToString("O"))}&end={Uri.EscapeDataString(now.AddMinutes(30).ToString("O"))}";
            var overlap = await client.GetFromJsonAsync<JsonElement[]>("/api/parking-spaces" + query);
            Assert.False(Assert.Single(overlap!).GetProperty("isAvailable").GetBoolean());

            reservation.Status = ReservationStatus.Cancelled;
            reservation.CancelledAt = now;
            await db.SaveChangesAsync();
            var cancelled = await client.GetFromJsonAsync<JsonElement[]>("/api/parking-spaces" + query);
            Assert.True(Assert.Single(cancelled!).GetProperty("isAvailable").GetBoolean());
        }
        finally
        {
            db.Reservations.Remove(reservation);
            db.Vehicles.Remove(vehicle);
            db.ParkingSpaces.Remove(space);
            db.Users.Remove(owner);
            await db.SaveChangesAsync();
        }
    }
}
