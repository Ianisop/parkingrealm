using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class CancellationEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Owner_cancellation_preserves_history_and_allows_rebooking()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        var created = await data.Client.PostAsJsonAsync("/api/reservations", ReservationEndpointTests.Request(data));
        var reservation = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = reservation.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await data.Client.PostAsync($"/api/reservations/{id}/cancel", null)).StatusCode);
        var history = await data.Client.GetFromJsonAsync<JsonElement[]>("/api/reservations");
        Assert.Equal("Cancelled", Assert.Single(history!).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Created,
            (await data.Client.PostAsJsonAsync("/api/reservations", ReservationEndpointTests.Request(data))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await data.Client.PostAsync($"/api/reservations/{id}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task Foreign_missing_and_anonymous_cancellations_are_rejected()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        var created = await data.Client.PostAsJsonAsync("/api/reservations", ReservationEndpointTests.Request(data));
        var reservation = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = reservation.GetProperty("id").GetInt32();
        using var other = await BookingCustomer.LoginAsync(factory);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/reservations/{id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await data.Client.PostAsync("/api/reservations/0/cancel", null)).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"/api/reservations/{id}/cancel", null)).StatusCode);
        Assert.Equal("Active", Assert.Single((await data.Client.GetFromJsonAsync<JsonElement[]>("/api/reservations"))!)
            .GetProperty("status").GetString());
    }
}
