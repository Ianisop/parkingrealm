using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class VehicleEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Anonymous_requests_are_rejected()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/vehicles")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/vehicles", new { registrationNumber = "AB12345", isElectric = false })).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567")]
    public async Task Invalid_plate_is_rejected(string? registrationNumber)
    {
        using var client = await BookingCustomer.LoginAsync(factory);
        var response = await client.PostAsJsonAsync("/api/vehicles", new { registrationNumber, isElectric = false });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Plate_is_trimmed_and_duplicates_are_case_insensitive()
    {
        using var client = await BookingCustomer.LoginAsync(factory);
        var response = await client.PostAsJsonAsync("/api/vehicles", new { registrationNumber = " ab12345 ", isElectric = false });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ab12345", created.GetProperty("registrationNumber").GetString());

        var duplicate = await client.PostAsJsonAsync("/api/vehicles", new { registrationNumber = "AB12345", isElectric = true });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        var vehicles = await client.GetFromJsonAsync<JsonElement[]>("/api/vehicles");
        Assert.False(Assert.Single(vehicles!).GetProperty("isElectric").GetBoolean());
    }

    [Fact]
    public async Task Vehicles_are_only_listed_for_their_owner()
    {
        using var owner = await BookingCustomer.LoginAsync(factory);
        using var other = await BookingCustomer.LoginAsync(factory);
        Assert.Equal(HttpStatusCode.Created,
            (await owner.PostAsJsonAsync("/api/vehicles", new { registrationNumber = "AB12345", isElectric = false })).StatusCode);

        Assert.Empty((await other.GetFromJsonAsync<JsonElement[]>("/api/vehicles"))!);
        Assert.Single((await owner.GetFromJsonAsync<JsonElement[]>("/api/vehicles"))!);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Electric_classification_is_returned_and_persisted(bool isElectric)
    {
        using var client = await BookingCustomer.LoginAsync(factory);
        var response = await client.PostAsJsonAsync("/api/vehicles", new { registrationNumber = "EV12345", isElectric });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(isElectric, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isElectric").GetBoolean());
        Assert.Equal(isElectric, Assert.Single((await client.GetFromJsonAsync<JsonElement[]>("/api/vehicles"))!)
            .GetProperty("isElectric").GetBoolean());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_null_electric_classification_is_rejected(bool includeNull)
    {
        using var client = await BookingCustomer.LoginAsync(factory);
        var request = new Dictionary<string, object?> { ["registrationNumber"] = "EV12345" };
        if (includeNull) request["isElectric"] = null;
        var response = await client.PostAsJsonAsync("/api/vehicles", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("isElectric", out _));
    }

    [Fact]
    public async Task Unknown_plate_is_rejected_without_persisting()
    {
        using var client = await BookingCustomer.LoginAsync(factory);
        var response = await client.PostAsJsonAsync("/api/vehicles", new { registrationNumber = FakeVehicleRegistry.UnknownPlate });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("registrationNumber", await response.Content.ReadAsStringAsync());
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/vehicles"))!);
    }

    [Fact]
    public async Task Unavailable_registry_is_reported_without_persisting()
    {
        using var client = await BookingCustomer.LoginAsync(factory);
        var response = await client.PostAsJsonAsync("/api/vehicles", new { registrationNumber = FakeVehicleRegistry.UnavailablePlate });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/vehicles"))!);
    }

    [Fact]
    public async Task Concurrent_duplicate_registrations_create_one_vehicle()
    {
        using var client = await BookingCustomer.LoginAsync(factory);
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            client.PostAsJsonAsync("/api/vehicles", new { registrationNumber = "EF12345" })));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
        Assert.Single((await client.GetFromJsonAsync<JsonElement[]>("/api/vehicles"))!);
    }

    [Fact]
    public async Task Database_failure_is_an_error_and_persists_nothing()
    {
        using var client = await BookingCustomer.LoginAsync(factory);
        var cookie = (await client.GetAsync("/api/auth/me")).RequestMessage!.Headers.GetValues("Cookie");
        await using var unavailable = new ApiFactory(ApiFactory.UnreachableConnectionString);
        using var failing = unavailable.CreateClient();
        failing.DefaultRequestHeaders.Add("Cookie", cookie);
        failing.DefaultRequestHeaders.Add("X-CSRF-TOKEN", client.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN"));
        var response = await failing.PostAsJsonAsync("/api/vehicles", new { registrationNumber = "AB12345" });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("SqlException", await response.Content.ReadAsStringAsync());
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/vehicles"))!);
    }
}
