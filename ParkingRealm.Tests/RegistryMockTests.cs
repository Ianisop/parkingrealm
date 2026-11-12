using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ParkingRealm.RegistryMock;

namespace ParkingRealm.Tests;

public class RegistryMockTests : IAsyncLifetime
{
    private Microsoft.AspNetCore.Builder.WebApplication app = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        app = RegistryMockApp.Build(["--urls", "http://127.0.0.1:0"]);
        await app.StartAsync();
        var address = app.Urls.First();
        client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await app.DisposeAsync();
    }

    [Theory]
    [InlineData("AB12345")]
    [InlineData("CD12345")]
    [InlineData("EF12345")]
    [InlineData("ef12345")]
    public async Task Known_plates_are_found_case_insensitively(string plate)
    {
        var response = await client.GetAsync($"/vehicles/{plate}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(plate.ToUpperInvariant(), body.GetProperty("registrationNumber").GetString());
    }

    [Fact]
    public async Task Unknown_plate_returns_not_found() =>
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/vehicles/ZZ99999")).StatusCode);
}
