using System.Net;
using System.Net.Http.Json;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class BookingSecurityTests(ApiFactory factory)
{
    [Fact]
    public async Task Missing_forged_and_other_user_tokens_are_rejected()
    {
        using var client = await BookingCustomer.LoginAsync(factory);
        using var other = await BookingCustomer.LoginAsync(factory);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/vehicles", new { registrationNumber = "AB12345" })).StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "forged");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/vehicles", new { registrationNumber = "AB12345" })).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", other.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN"));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/vehicles", new { registrationNumber = "AB12345" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await other.PostAsJsonAsync("/api/vehicles", new { registrationNumber = "AB12345" })).StatusCode);
    }

    [Fact]
    public async Task Reservation_writes_also_require_verification()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        data.Client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await data.Client.PostAsJsonAsync("/api/reservations", ReservationEndpointTests.Request(data))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await data.Client.PostAsync("/api/reservations/0/cancel", null)).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/antiforgery")).StatusCode);
    }
}
