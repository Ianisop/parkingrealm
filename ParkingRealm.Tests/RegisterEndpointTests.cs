using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

public class RegisterEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "Passw0rd!";

    private static object ValidRequest(string? email = null) => new
    {
        email = email ?? $"{Guid.NewGuid():N}@example.com",
        password = Password,
        firstName = "Anna",
        lastName = "Hansen",
        phoneNumber = "+4512345678"
    };

    private static async Task<JsonElement> ErrorsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors");
    }

    [Fact]
    public async Task Valid_registration_stores_all_fields_and_a_password_hash()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", ValidRequest(email));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Password, responseBody);
        Assert.DoesNotContain("passwordHash", responseBody, StringComparison.OrdinalIgnoreCase);

        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.Equal("Anna", user.FirstName);
        Assert.Equal("Hansen", user.LastName);
        Assert.Equal("+4512345678", user.PhoneNumber);
        Assert.NotNull(user.PasswordHash);
        Assert.NotEqual(Password, user.PasswordHash);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("password")]
    [InlineData("firstName")]
    [InlineData("lastName")]
    [InlineData("phoneNumber")]
    public async Task Missing_field_is_rejected_and_no_user_is_created(string missingField)
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var request = JsonSerializer.SerializeToNode(ValidRequest(email))!.AsObject();
        request.Remove(missingField);

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ErrorsAsync(response)).TryGetProperty(missingField, out _));
        await AssertNoUserAsync(email, missingField);
    }

    [Fact]
    public async Task Malformed_email_is_rejected()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", ValidRequest("not-an-email"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ErrorsAsync(response)).TryGetProperty("email", out _));
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("0045 12345678")]
    [InlineData("+")]
    [InlineData("+45abc")]
    public async Task Phone_number_without_country_code_is_rejected_and_no_user_is_created(string phone)
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var request = new { email, password = Password, firstName = "Anna", lastName = "Hansen", phoneNumber = phone };

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ErrorsAsync(response)).TryGetProperty("phoneNumber", out _));
        await AssertNoUserAsync(email, phone);
    }

    [Fact]
    public async Task Weak_password_is_rejected_and_no_user_is_created()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var request = new { email, password = "abc", firstName = "Anna", lastName = "Hansen", phoneNumber = "+4512345678" };

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ErrorsAsync(response)).TryGetProperty("password", out _));
        await AssertNoUserAsync(email, "weak password");
    }

    [Fact]
    public async Task Duplicate_email_in_different_letter_case_is_rejected()
    {
        var local = Guid.NewGuid().ToString("N");
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/auth/register", ValidRequest($"{local}@example.com"))).StatusCode);

        var response = await client.PostAsJsonAsync("/api/auth/register", ValidRequest($"{local.ToUpperInvariant()}@EXAMPLE.com"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var email = (await ErrorsAsync(response)).GetProperty("email");
        Assert.Contains("already in use", email[0].GetString());
    }

    [Fact]
    public async Task Database_failure_returns_generic_500_without_leaking_details()
    {
        await using var unreachable = new ApiFactory(ApiFactory.UnreachableConnectionString);

        var response = await unreachable.CreateClient().PostAsJsonAsync("/api/auth/register", ValidRequest());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SqlException", body);
        Assert.DoesNotContain("Server=", body);
        Assert.DoesNotContain("localhost", body);
        Assert.DoesNotContain("   at ", body);
    }

    private async Task AssertNoUserAsync(string email, string because)
    {
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
        Assert.True(user is null, $"A user was created despite: {because}");
    }
}
