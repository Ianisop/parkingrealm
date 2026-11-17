using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class ReservationEndpointTests(ApiFactory factory)
{
    internal static object Request(ReservationTestData data, string mode = "manual") => new
    {
        vehicleId = data.VehicleId, start = data.Start.ToString("O"), end = data.Start.AddHours(1).ToString("O"),
        mode, parkingSpaceId = mode == "manual" ? (int?)data.SpaceId : null
    };

    [Theory]
    [InlineData("manual")]
    [InlineData("automatic")]
    public async Task Valid_booking_is_persisted_and_only_owner_can_list_it(string mode)
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        var response = await data.Client.PostAsJsonAsync("/api/reservations", Request(data, mode));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(data.SpaceId, created.GetProperty("parkingSpaceId").GetInt32());
        Assert.Equal("Active", created.GetProperty("status").GetString());
        Assert.Single((await data.Client.GetFromJsonAsync<JsonElement[]>("/api/reservations"))!);
        using var other = await BookingCustomer.LoginAsync(factory);
        Assert.Empty((await other.GetFromJsonAsync<JsonElement[]>("/api/reservations"))!);
    }

    [Fact]
    public async Task Stale_availability_and_foreign_vehicle_are_rejected()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.Created,
            (await data.Client.PostAsJsonAsync("/api/reservations", Request(data))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await data.Client.PostAsJsonAsync("/api/reservations", Request(data))).StatusCode);
        using var other = await BookingCustomer.LoginAsync(factory);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await other.PostAsJsonAsync("/api/reservations", Request(data))).StatusCode);
    }

    [Fact]
    public async Task Invalid_criteria_and_anonymous_access_are_rejected()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await data.Client.PostAsJsonAsync("/api/reservations", new { vehicleId = data.VehicleId })).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/reservations")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/reservations", Request(data))).StatusCode);
    }

    [Fact]
    public async Task Database_failure_returns_error_not_confirmation()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        var me = await data.Client.GetAsync("/api/auth/me");
        var cookie = me.RequestMessage!.Headers.GetValues("Cookie");
        await using var unavailable = new ApiFactory(ApiFactory.UnreachableConnectionString);
        using var client = unavailable.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", data.Client.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN"));
        var response = await client.PostAsJsonAsync("/api/reservations", Request(data));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SqlException", body);
        Assert.DoesNotContain("Created", body);
    }
}
