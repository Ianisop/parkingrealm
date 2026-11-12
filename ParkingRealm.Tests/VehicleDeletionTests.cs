using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class VehicleDeletionTests(ApiFactory factory)
{
    private static async Task<int> RegisterAsync(HttpClient client, string plate)
    {
        var response = await client.PostAsJsonAsync("/api/vehicles", new { registrationNumber = plate });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private static async Task<JsonElement[]> ListAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement[]>("/api/vehicles"))!;

    private async Task AddReservationAsync(ReservationTestData data, ReservationStatus status, TimeSpan startOffset)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var start = DateTimeOffset.UtcNow + startOffset;
        db.Reservations.Add(new Reservation
        {
            OwnerId = data.OwnerId, VehicleId = data.VehicleId, ParkingSpaceId = data.SpaceId,
            Start = start, End = start.AddHours(1), Status = status, CreatedAt = DateTimeOffset.UtcNow,
            CancelledAt = status == ReservationStatus.Cancelled ? DateTimeOffset.UtcNow : null
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Owner_can_delete_an_unreferenced_vehicle()
    {
        using var client = await BookingCustomer.LoginAsync(factory);
        var id = await RegisterAsync(client, "AB12345");
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/vehicles/{id}")).StatusCode);
        Assert.Empty(await ListAsync(client));
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/vehicles/{id}")).StatusCode);
    }

    [Fact]
    public async Task Other_users_and_missing_vehicles_get_the_same_not_found()
    {
        using var owner = await BookingCustomer.LoginAsync(factory);
        using var other = await BookingCustomer.LoginAsync(factory);
        var id = await RegisterAsync(owner, "AB12345");

        var foreign = await other.DeleteAsync($"/api/vehicles/{id}");
        var missing = await other.DeleteAsync("/api/vehicles/2147483647");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await foreign.Content.ReadAsStringAsync());
        Assert.Single(await ListAsync(owner));
    }

    [Fact]
    public async Task Anonymous_and_unverified_deletes_are_rejected()
    {
        using var client = await BookingCustomer.LoginAsync(factory);
        var id = await RegisterAsync(client, "AB12345");
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"/api/vehicles/{id}")).StatusCode);

        var token = client.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN").Single();
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/vehicles/{id}")).StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "forged");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/vehicles/{id}")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        Assert.Single(await ListAsync(client));
    }

    [Theory]
    [InlineData(ReservationStatus.Active, 24)]
    [InlineData(ReservationStatus.Cancelled, 24)]
    [InlineData(ReservationStatus.Active, -48)]
    [InlineData(ReservationStatus.Cancelled, -48)]
    public async Task Vehicle_with_any_reservation_cannot_be_deleted(ReservationStatus status, int startHours)
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        await AddReservationAsync(data, status, TimeSpan.FromHours(startHours));

        var response = await data.Client.DeleteAsync($"/api/vehicles/{data.VehicleId}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(await ListAsync(data.Client));
        Assert.Single((await data.Client.GetFromJsonAsync<JsonElement[]>("/api/reservations"))!);
    }

    [Fact]
    public async Task Storage_failure_is_an_error_and_keeps_the_vehicle()
    {
        using var client = await BookingCustomer.LoginAsync(factory);
        var id = await RegisterAsync(client, "AB12345");
        var cookie = (await client.GetAsync("/api/auth/me")).RequestMessage!.Headers.GetValues("Cookie");
        await using var unavailable = new ApiFactory(ApiFactory.UnreachableConnectionString);
        using var failing = unavailable.CreateClient();
        failing.DefaultRequestHeaders.Add("Cookie", cookie);
        failing.DefaultRequestHeaders.Add("X-CSRF-TOKEN", client.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN"));

        var response = await failing.DeleteAsync($"/api/vehicles/{id}");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("SqlException", await response.Content.ReadAsStringAsync());
        Assert.Single(await ListAsync(client));
    }

    [Fact]
    public async Task Concurrent_booking_and_deletion_never_both_succeed()
    {
        for (var round = 0; round < 6; round++)
        {
            await using var data = await ReservationTestData.CreateAsync(factory);
            var gate = new TaskCompletionSource();
            async Task<HttpResponseMessage> Run(Func<Task<HttpResponseMessage>> send)
            {
                await gate.Task;
                return await send();
            }

            var delete = Run(() => data.Client.DeleteAsync($"/api/vehicles/{data.VehicleId}"));
            var book = Run(() => data.Client.PostAsJsonAsync("/api/reservations", ReservationEndpointTests.Request(data)));
            gate.SetResult();
            var (deleted, booked) = (await delete, await book);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var vehicleExists = await db.Vehicles.AnyAsync(v => v.Id == data.VehicleId);
            var reservations = await db.Reservations.CountAsync(r => r.VehicleId == data.VehicleId);

            if (deleted.StatusCode == HttpStatusCode.NoContent)
            {
                Assert.Equal(HttpStatusCode.BadRequest, booked.StatusCode);
                Assert.False(vehicleExists);
                Assert.Equal(0, reservations);
            }
            else
            {
                Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
                Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
                Assert.True(vehicleExists);
                Assert.Equal(1, reservations);
            }
        }
    }
}
