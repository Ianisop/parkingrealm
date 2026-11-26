using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class PasswordResetEndpointTests(ApiFactory factory)
{
    private const string Password = "Passw0rd!";
    private const string NewPassword = "N3w-Passw0rd!";

    private HttpClient NewClient(ApiFactory? from = null) =>
        (from ?? factory).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static async Task<string> RegisterAsync(HttpClient client)
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email, password = Password, firstName = "Anna", lastName = "Hansen", phoneNumber = "+4512345678"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return email;
    }

    private static Task<HttpResponseMessage> ForgotAsync(HttpClient client, string? email) =>
        client.PostAsJsonAsync("/api/auth/forgot-password", new { email });

    private static Task<HttpResponseMessage> ResetAsync(HttpClient client, string email, string token, string? password) =>
        client.PostAsJsonAsync("/api/auth/reset-password", new { email, token, newPassword = password });

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    private async Task<(string Email, string Token)> RequestLinkAsync(HttpClient client, string email)
    {
        Assert.Equal(HttpStatusCode.OK, (await ForgotAsync(client, email)).StatusCode);
        return factory.ResetSender.LastLinkFor(email);
    }

    private static async Task<string> BodyWithoutTraceIdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return string.Join("|", body.EnumerateObject().Where(p => p.Name != "traceId").Select(p => $"{p.Name}={p.Value}"));
    }

    private async Task MarkDeletedAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await userManager.FindByEmailAsync(email))!;
        user.DeletedAt = DateTimeOffset.UtcNow;
        Assert.True((await userManager.UpdateAsync(user)).Succeeded);
    }

    // 2.1 ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Forgot_password_for_an_existing_account_issues_a_link_but_never_returns_the_token()
    {
        var client = NewClient();
        var email = await RegisterAsync(client);

        var response = await ForgotAsync(client, email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var link = Assert.Single(factory.ResetSender.LinksFor(email));
        var (_, token) = factory.ResetSender.LastLinkFor(email);
        Assert.StartsWith("http://localhost:3000/?resetToken=", link);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(token, body);
        Assert.DoesNotContain("resetToken", body);
    }

    [Fact]
    public async Task Forgot_password_gives_the_same_response_for_unknown_and_deleted_accounts_and_issues_no_link()
    {
        var client = NewClient();
        var existing = await RegisterAsync(client);
        var deleted = await RegisterAsync(client);
        await MarkDeletedAsync(deleted);
        var unknown = $"{Guid.NewGuid():N}@example.com";

        var forExisting = await ForgotAsync(client, existing);
        var forUnknown = await ForgotAsync(client, unknown);
        var forDeleted = await ForgotAsync(client, deleted);

        Assert.Equal(HttpStatusCode.OK, forUnknown.StatusCode);
        var existingBody = await BodyWithoutTraceIdAsync(forExisting);
        Assert.Equal(existingBody, await BodyWithoutTraceIdAsync(forUnknown));
        Assert.Equal(existingBody, await BodyWithoutTraceIdAsync(forDeleted));
        Assert.Empty(factory.ResetSender.LinksFor(unknown));
        Assert.Empty(factory.ResetSender.LinksFor(deleted));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task Forgot_password_with_missing_or_malformed_email_returns_400(string? email)
    {
        var response = await ForgotAsync(NewClient(), email);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Administrator_can_request_a_reset_for_their_own_account()
    {
        using var admin = await AdministratorRoleTests.LoginAdministratorAsync(factory);
        var email = (await admin.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("email").GetString()!;
        using var anonymous = NewClient();

        Assert.Equal(HttpStatusCode.OK, (await ForgotAsync(anonymous, email)).StatusCode);

        Assert.NotEmpty(factory.ResetSender.LinksFor(email));
    }

    // 2.2 ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Reset_sets_the_new_password_hashed_and_the_old_password_stops_working()
    {
        var client = NewClient();
        var email = await RegisterAsync(client);
        var (linkEmail, token) = await RequestLinkAsync(client, email);

        var response = await ResetAsync(client, linkEmail, token, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, email, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), email, Password)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var stored = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email))!;
        Assert.NotEqual(NewPassword, stored.PasswordHash);
        Assert.DoesNotContain(NewPassword, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reset_with_a_token_for_another_account_or_a_tampered_token_changes_nothing()
    {
        var client = NewClient();
        var first = await RegisterAsync(client);
        var second = await RegisterAsync(client);
        var (_, firstToken) = await RequestLinkAsync(client, first);

        var wrongAccount = await ResetAsync(client, second, firstToken, NewPassword);
        var tampered = await ResetAsync(client, first, firstToken[..^2] + "xx", NewPassword);
        var garbage = await ResetAsync(client, first, "not a token!", NewPassword);
        var unknown = await ResetAsync(client, $"{Guid.NewGuid():N}@example.com", firstToken, NewPassword);

        foreach (var response in new[] { wrongAccount, tampered, garbage, unknown })
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(await BodyWithoutTraceIdAsync(wrongAccount), await BodyWithoutTraceIdAsync(unknown));
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), first, Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), second, Password)).StatusCode);
    }

    [Fact]
    public async Task Rejected_password_leaves_the_link_usable_for_a_valid_password()
    {
        var client = NewClient();
        var email = await RegisterAsync(client);
        var (_, token) = await RequestLinkAsync(client, email);

        var weak = await ResetAsync(client, email, token, "short");

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.True((await weak.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("password", out _));
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), email, Password)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(client, email, token, NewPassword)).StatusCode);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("token")]
    [InlineData("newPassword")]
    public async Task Reset_with_a_missing_field_returns_400(string missingField)
    {
        var client = NewClient();
        var email = await RegisterAsync(client);
        var (_, token) = await RequestLinkAsync(client, email);
        var request = JsonSerializer.SerializeToNode(new { email, token, newPassword = NewPassword })!.AsObject();
        request.Remove(missingField);

        var response = await client.PostAsJsonAsync("/api/auth/reset-password", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), email, Password)).StatusCode);
    }

    [Fact]
    public async Task Used_link_cannot_be_used_again()
    {
        var client = NewClient();
        var email = await RegisterAsync(client);
        var (_, token) = await RequestLinkAsync(client, email);
        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(client, email, token, NewPassword)).StatusCode);

        var reuse = await ResetAsync(client, email, token, "An0ther-Passw0rd!");

        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Expired_link_is_refused()
    {
        var client = NewClient();
        var email = await RegisterAsync(client);
        // The Testing environment skips the development seeding, which would fill the shared parking inventory.
        await using var expiring = factory.WithWebHostBuilder(b =>
            b.UseEnvironment("Testing").UseSetting("PasswordReset:TokenLifespanMinutes", "-1"));
        using var expiringClient = expiring.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await ForgotAsync(expiringClient, email)).StatusCode);
        var (_, token) = factory.ResetSender.LastLinkFor(email);

        var response = await ResetAsync(expiringClient, email, token, NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), email, Password)).StatusCode);
    }

    [Fact]
    public async Task Reset_ends_an_active_session_of_the_account()
    {
        var session = NewClient();
        var email = await RegisterAsync(session);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(session, email, Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/auth/me")).StatusCode);
        var other = NewClient();
        var (_, token) = await RequestLinkAsync(other, email);

        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(other, email, token, NewPassword)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Deleted_account_cannot_be_reset()
    {
        var client = NewClient();
        var email = await RegisterAsync(client);
        var (_, token) = await RequestLinkAsync(client, email);
        await MarkDeletedAsync(email);

        var response = await ResetAsync(client, email, token, NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // 2.3 ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Database_failure_returns_generic_500_without_technical_details()
    {
        await using var unreachable = new ApiFactory(ApiFactory.UnreachableConnectionString);
        using var client = unreachable.CreateClient();

        var forgot = await ForgotAsync(client, "a@example.com");
        var reset = await ResetAsync(client, "a@example.com", "dG9rZW4", NewPassword);

        foreach (var response in new[] { forgot, reset })
        {
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("SqlException", body);
            Assert.DoesNotContain("Server=", body);
            Assert.DoesNotContain("localhost", body);
            Assert.DoesNotContain("   at ", body);
        }
    }
}
