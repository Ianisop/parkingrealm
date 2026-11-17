using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ParkingRealm.Tests;

internal static class BookingCustomer
{
    public static async Task<HttpClient> LoginAsync(ApiFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var email = $"{Guid.NewGuid():N}@example.com";
        const string password = "Passw0rd!";
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email, password, firstName = "Anna", lastName = "Hansen", phoneNumber = "+4512345678"
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = await client.GetFromJsonAsync<JsonElement>("/api/antiforgery");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
        return client;
    }
}
