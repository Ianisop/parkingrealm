using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class LogoutEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Ordinary_user_logout_ends_the_session()
    {
        using var client = await BookingCustomer.LoginAsync(factory);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/vehicles")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var cookie = Assert.Single(logout.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Identity.Application="));
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        var protectedResponse = await client.GetAsync("/api/vehicles");
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
        Assert.Null(protectedResponse.Headers.Location);
    }

    [Fact]
    public async Task Administrator_logout_removes_administrator_access()
    {
        using var client = await AdministratorRoleTests.LoginAdministratorAsync(factory);
        var before = (await client.GetAsync("/api/admin/booking-statistics")).StatusCode;
        Assert.DoesNotContain(before, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });

        var logout = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/booking-statistics")).StatusCode);
    }

    [Fact]
    public async Task Logout_without_a_session_and_repeated_logout_succeed()
    {
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsync("/api/auth/logout", null)).StatusCode);

        using var client = await BookingCustomer.LoginAsync(factory);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task User_can_log_in_again_after_logout()
    {
        using var client = await BookingCustomer.LoginAsync(factory);
        var email = (await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("email").GetString();
        await client.PostAsync("/api/auth/logout", null);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Passw0rd!" });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_does_not_accept_get()
    {
        using var client = await BookingCustomer.LoginAsync(factory);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.GetAsync("/api/auth/logout")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }
}
