using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class LoginEndpointTests(ApiFactory factory)
{
    private const string Password = "Passw0rd!";

    // Each test gets its own client so cookies do not leak between tests.
    private HttpClient NewClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private async Task<string> RegisterAsync(HttpClient client)
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = Password,
            firstName = "Anna",
            lastName = "Hansen",
            phoneNumber = "+4512345678"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return email;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    // Problem responses differ in traceId only; compare everything else.
    private static async Task<string> BodyWithoutTraceIdAsync(HttpResponseMessage response)
    {
        var body = await JsonAsync(response);
        return string.Join("|", body.EnumerateObject().Where(p => p.Name != "traceId").Select(p => $"{p.Name}={p.Value}"));
    }

    [Fact]
    public async Task Valid_login_returns_the_user_and_me_works_afterwards()
    {
        var client = NewClient();
        var email = await RegisterAsync(client);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = await login.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Password, loginBody);
        Assert.DoesNotContain("hash", loginBody, StringComparison.OrdinalIgnoreCase);
        Assert.True(login.Headers.TryGetValues("Set-Cookie", out var cookies));
        var cookie = Assert.Single(cookies, c => c.StartsWith(".AspNetCore.Identity.Application="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);

        var me = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var user = await JsonAsync(me);
        Assert.Equal(email, user.GetProperty("email").GetString());
        Assert.Equal("Anna", user.GetProperty("firstName").GetString());
        Assert.Equal("Hansen", user.GetProperty("lastName").GetString());
    }

    [Fact]
    public async Task Email_in_different_letter_case_logs_in()
    {
        var client = NewClient();
        var email = await RegisterAsync(client);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = email.ToUpperInvariant(), password = Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_give_identical_401_and_no_access()
    {
        var client = NewClient();
        var email = await RegisterAsync(client);

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Wr0ng-password" });
        var unknownEmail = await client.PostAsJsonAsync("/api/auth/login", new { email = $"{Guid.NewGuid():N}@example.com", password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(await BodyWithoutTraceIdAsync(wrongPassword), await BodyWithoutTraceIdAsync(unknownEmail));
        Assert.False(wrongPassword.Headers.Contains("Set-Cookie"));
        Assert.False(unknownEmail.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("password")]
    public async Task Missing_field_is_rejected_with_400_and_no_access(string missingField)
    {
        var client = NewClient();
        var email = await RegisterAsync(client);
        var request = JsonSerializer.SerializeToNode(new { email, password = Password })!.AsObject();
        request.Remove(missingField);

        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty(missingField, out _));
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Me_without_login_returns_401_without_redirect()
    {
        var response = await NewClient().GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Logged_in_user_has_no_role_claims()
    {
        var client = NewClient();
        var email = await RegisterAsync(client);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        var principal = await signInManager.CreateUserPrincipalAsync(user);

        Assert.Empty(principal.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public async Task Database_failure_returns_generic_500_with_no_cookie_and_no_leak()
    {
        await using var unreachable = new ApiFactory(ApiFactory.UnreachableConnectionString);

        var response = await unreachable.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "a@example.com", password = Password });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SqlException", body);
        Assert.DoesNotContain("Server=", body);
        Assert.DoesNotContain("localhost", body);
        Assert.DoesNotContain("   at ", body);
    }
}
