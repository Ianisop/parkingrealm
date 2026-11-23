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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task Invalid_or_unknown_preferred_locations_are_rejected(int preferredSpaceId)
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        var response = await data.Client.PostAsJsonAsync("/api/reservations", new
        {
            vehicleId = data.VehicleId, start = data.Start.ToString("O"), end = data.Start.AddHours(1).ToString("O"),
            mode = "automatic", preferredSpaceId
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("preferredSpaceId", out _));
        Assert.Empty((await data.Client.GetFromJsonAsync<JsonElement[]>("/api/reservations"))!);
    }

    [Fact]
    public async Task Manual_assignment_does_not_accept_a_location_preference()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        var response = await data.Client.PostAsJsonAsync("/api/reservations", new
        {
            vehicleId = data.VehicleId, start = data.Start.ToString("O"), end = data.Start.AddHours(1).ToString("O"),
            mode = "manual", parkingSpaceId = data.SpaceId, preferredSpaceId = data.SpaceId
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await data.Client.GetFromJsonAsync<JsonElement[]>("/api/reservations"))!);
    }

    [Fact]
    public async Task Automatic_booking_uses_the_preference_and_falls_back_when_it_is_occupied()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        var preferred = await data.AddSpaceAsync(1);
        var request = new
        {
            vehicleId = data.VehicleId, start = data.Start.ToString("O"), end = data.Start.AddHours(1).ToString("O"),
            mode = "automatic", preferredSpaceId = preferred
        };
        var first = await data.Client.PostAsJsonAsync("/api/reservations", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(preferred, (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("parkingSpaceId").GetInt32());
        var fallback = await data.Client.PostAsJsonAsync("/api/reservations", request);
        Assert.Equal(HttpStatusCode.Created, fallback.StatusCode);
        Assert.Equal(data.SpaceId, (await fallback.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("parkingSpaceId").GetInt32());
    }

    [Theory]
    [InlineData("automatic", true, false)]
    [InlineData("automatic", false, true)]
    [InlineData("automatic", true, true)]
    [InlineData("manual", true, false)]
    [InlineData("manual", false, true)]
    [InlineData("manual", true, true)]
    public async Task Booking_requires_all_requested_features(string mode, bool accessible, bool charger)
    {
        await using var data = await ReservationTestData.CreateAsync(factory, isElectric: charger);
        var matching = await data.AddSpaceAsync(1, accessible, charger);
        var response = await data.Client.PostAsJsonAsync("/api/reservations", new
        {
            vehicleId = data.VehicleId, start = data.Start.ToString("O"), end = data.Start.AddHours(1).ToString("O"),
            mode, parkingSpaceId = mode == "manual" ? (int?)matching : null,
            preferredSpaceId = mode == "automatic" ? (int?)data.SpaceId : null,
            requiresAccessible = accessible, requiresCharger = charger
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(matching, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("parkingSpaceId").GetInt32());
    }

    [Theory]
    [InlineData("automatic", true, false)]
    [InlineData("automatic", false, true)]
    [InlineData("automatic", true, true)]
    [InlineData("manual", true, false)]
    [InlineData("manual", false, true)]
    [InlineData("manual", true, true)]
    public async Task Specialized_spaces_are_not_used_without_matching_requirements(string mode, bool accessible, bool charger)
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        var special = await data.AddSpaceAsync(1, accessible, charger);
        Assert.Equal(HttpStatusCode.Created, (await data.Client.PostAsJsonAsync("/api/reservations", Request(data))).StatusCode);
        var response = await data.Client.PostAsJsonAsync("/api/reservations", new
        {
            vehicleId = data.VehicleId, start = data.Start.ToString("O"), end = data.Start.AddHours(1).ToString("O"),
            mode, parkingSpaceId = mode == "manual" ? (int?)special : null
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single((await data.Client.GetFromJsonAsync<JsonElement[]>("/api/reservations"))!);
    }

    [Theory]
    [InlineData("automatic")]
    [InlineData("manual")]
    public async Task Separate_accessible_and_charging_spaces_do_not_satisfy_both_requirements(string mode)
    {
        await using var data = await ReservationTestData.CreateAsync(factory, isElectric: true);
        await data.AddSpaceAsync(1, isAccessible: true);
        var charging = await data.AddSpaceAsync(2, hasCharger: true);
        var response = await data.Client.PostAsJsonAsync("/api/reservations", new
        {
            vehicleId = data.VehicleId, start = data.Start.ToString("O"), end = data.Start.AddHours(1).ToString("O"),
            mode, parkingSpaceId = mode == "manual" ? (int?)charging : null,
            requiresAccessible = true, requiresCharger = true
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty((await data.Client.GetFromJsonAsync<JsonElement[]>("/api/reservations"))!);
    }

    [Theory]
    [InlineData("automatic")]
    [InlineData("manual")]
    public async Task Electric_vehicles_can_book_a_charger_without_explicitly_requiring_one(string mode)
    {
        await using var data = await ReservationTestData.CreateAsync(factory, isElectric: true);
        var charging = await data.AddSpaceAsync(1, hasCharger: true);
        Assert.Equal(HttpStatusCode.Created, (await data.Client.PostAsJsonAsync("/api/reservations", Request(data))).StatusCode);
        var response = await data.Client.PostAsJsonAsync("/api/reservations", new
        {
            vehicleId = data.VehicleId, start = data.Start.ToString("O"), end = data.Start.AddHours(1).ToString("O"),
            mode, parkingSpaceId = mode == "manual" ? (int?)charging : null
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(charging, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("parkingSpaceId").GetInt32());
    }

    [Theory]
    [InlineData("automatic", false)]
    [InlineData("manual", false)]
    [InlineData("automatic", true)]
    [InlineData("manual", true)]
    public async Task Non_electric_vehicle_cannot_claim_electric_status_in_booking(string mode, bool requiresCharger)
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        var charging = await data.AddSpaceAsync(1, hasCharger: true);
        Assert.Equal(HttpStatusCode.Created, (await data.Client.PostAsJsonAsync("/api/reservations", Request(data))).StatusCode);
        var response = await data.Client.PostAsJsonAsync("/api/reservations", new
        {
            vehicleId = data.VehicleId, start = data.Start.ToString("O"), end = data.Start.AddHours(1).ToString("O"),
            mode, parkingSpaceId = mode == "manual" ? (int?)charging : null,
            requiresCharger, isElectric = true, isElectricVehicle = true
        });
        Assert.Equal(requiresCharger ? HttpStatusCode.BadRequest : HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single((await data.Client.GetFromJsonAsync<JsonElement[]>("/api/reservations"))!);
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
