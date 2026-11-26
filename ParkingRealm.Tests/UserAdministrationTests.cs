using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class UserAdministrationTests(ApiFactory factory)
{
    private const string Password = "Passw0rd!";

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    private static object ValidUser(string? email = null) => new
    {
        email = email ?? NewEmail(), password = Password, firstName = "Bo", lastName = "Jensen", phoneNumber = "+4511223344"
    };

    private static async Task<HttpClient> LoginAdminAsync(ApiFactory factory)
    {
        var client = await AdministratorRoleTests.LoginAdministratorAsync(factory);
        var token = await client.GetFromJsonAsync<JsonElement>("/api/antiforgery");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
        return client;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<JsonElement> CreateAsync(HttpClient admin, string? email = null)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/users", ValidUser(email));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await JsonAsync(response);
    }

    private static async Task<bool> CanLoginAsync(ApiFactory factory, string email, string password)
    {
        using var client = factory.CreateClient();
        return (await client.PostAsJsonAsync("/api/auth/login", new { email, password })).StatusCode == HttpStatusCode.OK;
    }

    // 3.1 ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Listing_is_refused_for_anonymous_and_ordinary_users()
    {
        using var anonymous = factory.CreateClient();
        using var ordinary = await BookingCustomer.LoginAsync(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task Administrator_lists_and_searches_users_without_security_data()
    {
        using var admin = await LoginAdminAsync(factory);
        var email = NewEmail();
        var created = await CreateAsync(admin, email);

        var response = await admin.GetAsync($"/api/admin/users?search={Uri.EscapeDataString(email[..12])}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stamp", body, StringComparison.OrdinalIgnoreCase);
        var users = JsonSerializer.Deserialize<JsonElement[]>(body)!;
        var user = Assert.Single(users);
        Assert.Equal(created.GetProperty("id").GetString(), user.GetProperty("id").GetString());
        Assert.Equal(email, user.GetProperty("email").GetString());
        Assert.Equal("Bo", user.GetProperty("firstName").GetString());
        Assert.Equal("Jensen", user.GetProperty("lastName").GetString());
        Assert.Equal("+4511223344", user.GetProperty("phoneNumber").GetString());
        Assert.False(user.GetProperty("isAdministrator").GetBoolean());
    }

    [Fact]
    public async Task Listing_flags_administrators_and_filters_by_name()
    {
        using var admin = await LoginAdminAsync(factory);
        var me = await admin.GetFromJsonAsync<JsonElement>("/api/auth/me");

        var users = await admin.GetFromJsonAsync<JsonElement[]>("/api/admin/users?search=Hansen");

        var self = Assert.Single(users!, u => u.GetProperty("id").GetString() == me.GetProperty("id").GetString());
        Assert.True(self.GetProperty("isAdministrator").GetBoolean());
        Assert.DoesNotContain(users!, u => u.GetProperty("lastName").GetString() != "Hansen");
    }

    // 3.2 ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Created_user_is_stored_ordinary_and_can_log_in()
    {
        using var admin = await LoginAdminAsync(factory);
        var email = NewEmail();

        var created = await CreateAsync(admin, email);

        Assert.Equal(email, created.GetProperty("email").GetString());
        Assert.False(created.GetProperty("isAdministrator").GetBoolean());
        Assert.DoesNotContain("password", created.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(await CanLoginAsync(factory, email, Password));
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var stored = (await users.FindByEmailAsync(email))!;
        Assert.NotEqual(Password, stored.PasswordHash);
        Assert.Empty(await users.GetRolesAsync(stored));
    }

    [Theory]
    [InlineData("email")]
    [InlineData("password")]
    [InlineData("firstName")]
    [InlineData("lastName")]
    [InlineData("phoneNumber")]
    public async Task Create_with_missing_detail_is_rejected(string missing)
    {
        using var admin = await LoginAdminAsync(factory);
        var request = JsonSerializer.SerializeToNode(ValidUser())!.AsObject();
        request.Remove(missing);

        var response = await admin.PostAsJsonAsync("/api/admin/users", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty(missing, out _));
    }

    [Theory]
    [InlineData("email", "not-an-email")]
    [InlineData("phoneNumber", "12345678")]
    [InlineData("password", "short")]
    public async Task Create_with_invalid_detail_is_rejected_and_stores_nothing(string field, string value)
    {
        using var admin = await LoginAdminAsync(factory);
        var email = NewEmail();
        var request = JsonSerializer.SerializeToNode(ValidUser(email))!.AsObject();
        request[field] = value;

        var response = await admin.PostAsJsonAsync("/api/admin/users", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty(field, out _));
        using var scope = factory.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email));
    }

    [Fact]
    public async Task Create_with_duplicate_email_is_rejected()
    {
        using var admin = await LoginAdminAsync(factory);
        var email = NewEmail();
        await CreateAsync(admin, email);

        var response = await admin.PostAsJsonAsync("/api/admin/users", ValidUser(email.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty("email", out _));
    }

    [Fact]
    public async Task State_changing_requests_require_authorization_and_antiforgery_token()
    {
        using var ordinary = await BookingCustomer.LoginAsync(factory);
        using var admin = await LoginAdminAsync(factory);
        var created = await CreateAsync(admin);
        var id = created.GetProperty("id").GetString();
        var email = NewEmail();

        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync("/api/admin/users", ValidUser(email))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await ordinary.PutAsJsonAsync($"/api/admin/users/{id}", new { email, firstName = "X", lastName = "Y", phoneNumber = "+4511223344" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.DeleteAsync($"/api/admin/users/{id}")).StatusCode);

        admin.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/users", ValidUser(email))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PutAsJsonAsync($"/api/admin/users/{id}", new { email, firstName = "X", lastName = "Y", phoneNumber = "+4511223344" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/admin/users/{id}")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await users.FindByEmailAsync(email));
        Assert.NotNull(await users.FindByIdAsync(id!));
    }

    // 3.3 ---------------------------------------------------------------------------------------

    private static object Changes(string email) =>
        new { email, firstName = "Carl", lastName = "Nielsen", phoneNumber = "+4599887766" };

    [Fact]
    public async Task Edit_saves_changes_and_keeps_password_and_roles()
    {
        using var admin = await LoginAdminAsync(factory);
        var oldEmail = NewEmail();
        var id = (await CreateAsync(admin, oldEmail)).GetProperty("id").GetString();
        var newEmail = NewEmail();

        var response = await admin.PutAsJsonAsync($"/api/admin/users/{id}", Changes(newEmail));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal(newEmail, body.GetProperty("email").GetString());
        Assert.Equal("Carl", body.GetProperty("firstName").GetString());
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var stored = (await users.FindByIdAsync(id!))!;
        Assert.Equal(newEmail, stored.Email);
        Assert.Equal(newEmail, stored.UserName);
        Assert.Equal("Nielsen", stored.LastName);
        Assert.Equal("+4599887766", stored.PhoneNumber);
        Assert.Empty(await users.GetRolesAsync(stored));
        Assert.True(await CanLoginAsync(factory, newEmail, Password));
        Assert.False(await CanLoginAsync(factory, oldEmail, Password));
    }

    [Theory]
    [InlineData("email", "not-an-email")]
    [InlineData("email", "")]
    [InlineData("firstName", "")]
    [InlineData("lastName", " ")]
    [InlineData("phoneNumber", "12345678")]
    public async Task Edit_with_invalid_detail_is_rejected_and_saves_nothing(string field, string value)
    {
        using var admin = await LoginAdminAsync(factory);
        var email = NewEmail();
        var id = (await CreateAsync(admin, email)).GetProperty("id").GetString();
        var request = JsonSerializer.SerializeToNode(Changes(NewEmail()))!.AsObject();
        request[field] = value;

        var response = await admin.PutAsJsonAsync($"/api/admin/users/{id}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty(field, out _));
        using var scope = factory.Services.CreateScope();
        var stored = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(id!))!;
        Assert.Equal(email, stored.Email);
        Assert.Equal("Bo", stored.FirstName);
    }

    [Fact]
    public async Task Edit_to_an_email_of_another_user_is_rejected()
    {
        using var admin = await LoginAdminAsync(factory);
        var other = NewEmail();
        await CreateAsync(admin, other);
        var email = NewEmail();
        var id = (await CreateAsync(admin, email)).GetProperty("id").GetString();

        var response = await admin.PutAsJsonAsync($"/api/admin/users/{id}", Changes(other));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty("email", out _));
        using var scope = factory.Services.CreateScope();
        Assert.Equal(email, (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(id!))!.Email);
    }

    [Fact]
    public async Task Edit_of_unknown_user_returns_not_found()
    {
        using var admin = await LoginAdminAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/admin/users/{Guid.NewGuid():N}", Changes(NewEmail()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Administrator_accounts_cannot_be_edited_including_by_themselves()
    {
        using var admin = await LoginAdminAsync(factory);
        var me = await admin.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var id = me.GetProperty("id").GetString();

        var response = await admin.PutAsJsonAsync($"/api/admin/users/{id}", Changes(NewEmail()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var stored = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(id!))!;
        Assert.Equal(me.GetProperty("email").GetString(), stored.Email);
        Assert.Equal("Anna", stored.FirstName);
    }

    // 3.4 ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Delete_without_reservations_removes_the_user_and_vehicles()
    {
        using var admin = await LoginAdminAsync(factory);
        var email = NewEmail();
        var id = (await CreateAsync(admin, email)).GetProperty("id").GetString()!;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Vehicles.Add(new Vehicle { OwnerId = id, RegistrationNumber = "ZZ99999", NormalizedRegistrationNumber = "ZZ99999" });
            await db.SaveChangesAsync();
        }

        var response = await admin.DeleteAsync($"/api/admin/users/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var check = factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await checkDb.Users.AnyAsync(u => u.Id == id));
        Assert.False(await checkDb.Vehicles.AnyAsync(v => v.OwnerId == id));
        Assert.False(await CanLoginAsync(factory, email, Password));
        var listed = await admin.GetFromJsonAsync<JsonElement[]>($"/api/admin/users?search={Uri.EscapeDataString(email)}");
        Assert.Empty(listed!);
    }

    [Fact]
    public async Task Delete_with_reservations_anonymizes_and_keeps_history()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        var original = await data.Client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var email = original.GetProperty("email").GetString()!;
        Assert.Equal(HttpStatusCode.Created,
            (await data.Client.PostAsJsonAsync("/api/reservations", ReservationEndpointTests.Request(data))).StatusCode);
        using var admin = await LoginAdminAsync(factory);

        var response = await admin.DeleteAsync($"/api/admin/users/{data.OwnerId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == data.OwnerId);
        Assert.NotNull(user.DeletedAt);
        Assert.DoesNotContain(email, new[] { user.Email, user.UserName, user.NormalizedEmail, user.NormalizedUserName });
        Assert.DoesNotContain("Anna", user.FirstName);
        Assert.DoesNotContain("Hansen", user.LastName);
        Assert.Null(user.PhoneNumber);
        Assert.Null(user.PasswordHash);
        Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow.AddYears(100));
        var vehicle = await db.Vehicles.AsNoTracking().SingleAsync(v => v.OwnerId == data.OwnerId);
        Assert.DoesNotContain("AB12345", new[] { vehicle.RegistrationNumber, vehicle.NormalizedRegistrationNumber });
        Assert.Equal(1, await db.Reservations.CountAsync(r => r.OwnerId == data.OwnerId));
        Assert.False(await CanLoginAsync(factory, email, Password));
        Assert.Equal(HttpStatusCode.Unauthorized, (await data.Client.GetAsync("/api/auth/me")).StatusCode);
        var listed = await admin.GetFromJsonAsync<JsonElement[]>($"/api/admin/users?search={Uri.EscapeDataString(email)}");
        Assert.Empty(listed!);
    }

    [Fact]
    public async Task Anonymized_reservations_still_count_in_statistics()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.Created,
            (await data.Client.PostAsJsonAsync("/api/reservations", ReservationEndpointTests.Request(data))).StatusCode);
        using var admin = await LoginAdminAsync(factory);
        var path = $"/api/admin/booking-statistics?start={Uri.EscapeDataString(data.Start.AddHours(-1).ToString("O"))}&end={Uri.EscapeDataString(data.Start.AddHours(3).ToString("O"))}";
        var before = (await admin.GetFromJsonAsync<JsonElement>(path)).GetProperty("total").GetInt32();

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/users/{data.OwnerId}")).StatusCode);

        Assert.Equal(before, (await admin.GetFromJsonAsync<JsonElement>(path)).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Email_can_be_reused_after_deletion_with_reservations()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        var email = (await data.Client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("email").GetString()!;
        Assert.Equal(HttpStatusCode.Created,
            (await data.Client.PostAsJsonAsync("/api/reservations", ReservationEndpointTests.Request(data))).StatusCode);
        using var admin = await LoginAdminAsync(factory);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/users/{data.OwnerId}")).StatusCode);

        var created = await admin.PostAsJsonAsync("/api/admin/users", ValidUser(email));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True(await CanLoginAsync(factory, email, Password));
    }

    [Fact]
    public async Task Delete_and_edit_of_unknown_or_already_deleted_user_return_not_found()
    {
        using var admin = await LoginAdminAsync(factory);
        var id = (await CreateAsync(admin)).GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/users/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/admin/users/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/admin/users/{Guid.NewGuid():N}")).StatusCode);
    }

    [Fact]
    public async Task Deleted_with_reservations_user_is_not_found_by_edit_and_delete()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.Created,
            (await data.Client.PostAsJsonAsync("/api/reservations", ReservationEndpointTests.Request(data))).StatusCode);
        using var admin = await LoginAdminAsync(factory);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/users/{data.OwnerId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/admin/users/{data.OwnerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.PutAsJsonAsync($"/api/admin/users/{data.OwnerId}", Changes(NewEmail()))).StatusCode);
    }

    [Fact]
    public async Task Administrator_accounts_cannot_be_deleted_including_by_themselves()
    {
        using var admin = await LoginAdminAsync(factory);
        var id = (await admin.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetString()!;

        var response = await admin.DeleteAsync($"/api/admin/users/{id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var stored = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(id))!;
        Assert.Null(stored.DeletedAt);
        Assert.NotNull(stored.PasswordHash);
    }

    [Fact]
    public async Task Database_failure_is_a_generic_error()
    {
        using var admin = await LoginAdminAsync(factory);
        var cookie = (await admin.GetAsync("/api/auth/me")).RequestMessage!.Headers.GetValues("Cookie");
        await using var unavailable = new ApiFactory(ApiFactory.UnreachableConnectionString);
        using var failing = unavailable.CreateClient();
        failing.DefaultRequestHeaders.Add("Cookie", cookie);

        var response = await failing.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("SqlException", await response.Content.ReadAsStringAsync());
    }

    // Password reset link ------------------------------------------------------------------------

    private static Task<HttpResponseMessage> SendResetAsync(HttpClient client, string? id) =>
        client.PostAsync($"/api/admin/users/{id}/password-reset", null);

    [Fact]
    public async Task Reset_link_is_issued_for_ordinary_and_administrator_accounts_without_exposing_secrets()
    {
        using var admin = await LoginAdminAsync(factory);
        var ordinaryEmail = NewEmail();
        var ordinaryId = (await CreateAsync(admin, ordinaryEmail)).GetProperty("id").GetString();
        var me = await admin.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var adminEmail = me.GetProperty("email").GetString()!;

        var forOrdinary = await SendResetAsync(admin, ordinaryId);
        var forAdmin = await SendResetAsync(admin, me.GetProperty("id").GetString());

        Assert.Equal(HttpStatusCode.NoContent, forOrdinary.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, forAdmin.StatusCode);
        Assert.Empty(await forOrdinary.Content.ReadAsStringAsync());
        Assert.NotEmpty(factory.ResetSender.LinksFor(ordinaryEmail));
        Assert.NotEmpty(factory.ResetSender.LinksFor(adminEmail));
    }

    [Fact]
    public async Task Reset_link_keeps_the_old_password_until_the_user_completes_the_reset()
    {
        using var admin = await LoginAdminAsync(factory);
        var email = NewEmail();
        var id = (await CreateAsync(admin, email)).GetProperty("id").GetString();

        Assert.Equal(HttpStatusCode.NoContent, (await SendResetAsync(admin, id)).StatusCode);

        Assert.True(await CanLoginAsync(factory, email, Password));
        var (linkEmail, token) = factory.ResetSender.LastLinkFor(email);
        using var anonymous = factory.CreateClient();
        var reset = await anonymous.PostAsJsonAsync("/api/auth/reset-password", new { email = linkEmail, token, newPassword = "N3w-Passw0rd!" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.True(await CanLoginAsync(factory, email, "N3w-Passw0rd!"));
    }

    [Fact]
    public async Task Reset_link_for_unknown_or_deleted_user_is_not_found_and_issues_no_link()
    {
        using var admin = await LoginAdminAsync(factory);
        var email = NewEmail();
        var id = (await CreateAsync(admin, email)).GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/users/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await SendResetAsync(admin, id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendResetAsync(admin, Guid.NewGuid().ToString("N"))).StatusCode);
        Assert.Empty(factory.ResetSender.LinksFor(email));
    }

    [Fact]
    public async Task Reset_link_is_refused_for_anonymous_ordinary_and_missing_verification_token()
    {
        using var ordinary = await BookingCustomer.LoginAsync(factory);
        using var anonymous = factory.CreateClient();
        using var admin = await LoginAdminAsync(factory);
        var email = NewEmail();
        var id = (await CreateAsync(admin, email)).GetProperty("id").GetString();

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendResetAsync(anonymous, id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendResetAsync(ordinary, id)).StatusCode);
        admin.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await SendResetAsync(admin, id)).StatusCode);
        Assert.Empty(factory.ResetSender.LinksFor(email));
    }
}
