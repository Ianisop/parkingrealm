using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class AdministratorRoleTests(ApiFactory factory)
{
    internal static async Task<HttpClient> LoginAdministratorAsync(ApiFactory factory)
    {
        var client = await BookingCustomer.LoginAsync(factory);
        var current = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var email = current.GetProperty("email").GetString()!;
        using var scope = factory.Services.CreateScope();
        Assert.True((await AdministratorProvisioning.GrantAsync(scope.ServiceProvider, email)).Succeeded);
        Assert.True((await AdministratorProvisioning.GrantAsync(scope.ServiceProvider, email)).Succeeded);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Passw0rd!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    [Fact]
    public async Task Provisioned_role_is_reflected_after_login()
    {
        using var client = await LoginAdministratorAsync(factory);
        var current = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.True(current.GetProperty("isAdministrator").GetBoolean());
    }

    [Fact]
    public async Task Public_login_cannot_self_elevate()
    {
        using var client = await BookingCustomer.LoginAsync(factory);
        var current = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = current.GetProperty("email").GetString(), password = "Passw0rd!", role = "Administrator", isAdministrator = true
        });
        var login = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(login.GetProperty("isAdministrator").GetBoolean());
    }

    [Fact]
    public async Task Provisioning_requires_existing_account()
    {
        using var scope = factory.Services.CreateScope();
        var result = await AdministratorProvisioning.GrantAsync(scope.ServiceProvider, $"{Guid.NewGuid():N}@example.com");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "UserNotFound");
    }
}
