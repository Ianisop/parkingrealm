using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class BookingStatisticsTests(ApiFactory factory)
{
    private static string Path(DateTimeOffset start, DateTimeOffset end) =>
        $"/api/admin/booking-statistics?start={Uri.EscapeDataString(start.ToString("O"))}&end={Uri.EscapeDataString(end.ToString("O"))}";

    [Fact]
    public async Task Statistics_are_forbidden_without_trusted_role()
    {
        using var anonymous = factory.CreateClient();
        using var ordinary = await BookingCustomer.LoginAsync(factory);
        var path = Path(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Administrator_sees_totals_daily_patterns_and_cancellation_counts()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        var response = await data.Client.PostAsJsonAsync("/api/reservations", ReservationEndpointTests.Request(data));
        var first = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, (await data.Client.PostAsync($"/api/reservations/{first.GetProperty("id").GetInt32()}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await data.Client.PostAsJsonAsync("/api/reservations", ReservationEndpointTests.Request(data))).StatusCode);
        using var administrator = await AdministratorRoleTests.LoginAdministratorAsync(factory);
        var result = await administrator.GetFromJsonAsync<JsonElement>(Path(data.Start, data.Start.AddHours(2)));
        Assert.Equal(2, result.GetProperty("total").GetInt32());
        Assert.Equal(1, result.GetProperty("active").GetInt32());
        Assert.Equal(1, result.GetProperty("cancelled").GetInt32());
        Assert.True(result.GetProperty("hasData").GetBoolean());
        Assert.Single(result.GetProperty("days").EnumerateArray());
    }

    [Fact]
    public async Task Empty_data_and_invalid_intervals_are_explicit()
    {
        using var administrator = await AdministratorRoleTests.LoginAdministratorAsync(factory);
        var result = await administrator.GetFromJsonAsync<JsonElement>(Path(DateTimeOffset.Parse("2050-01-01T00:00:00Z"), DateTimeOffset.Parse("2050-01-02T00:00:00Z")));
        Assert.False(result.GetProperty("hasData").GetBoolean());
        Assert.Equal(0, result.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await administrator.GetAsync("/api/admin/booking-statistics")).StatusCode);
    }

    [Fact]
    public async Task Reporting_failure_is_not_presented_as_zero_data()
    {
        using var administrator = await AdministratorRoleTests.LoginAdministratorAsync(factory);
        var me = await administrator.GetAsync("/api/auth/me");
        await using var unavailable = new ApiFactory(ApiFactory.UnreachableConnectionString);
        using var client = unavailable.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", me.RequestMessage!.Headers.GetValues("Cookie"));
        var response = await client.GetAsync(Path(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
