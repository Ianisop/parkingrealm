using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ParkingRealm.Data;

namespace ParkingRealm.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register", RegisterAsync);
        app.MapPost("/api/auth/login", LoginAsync);
        app.MapGet("/api/auth/me", GetCurrentUserAsync).RequireAuthorization();
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var user = await userManager.FindByEmailAsync(request.Email!.Trim());
        if (user is null)
            return InvalidCredentials();

        var check = await signInManager.CheckPasswordSignInAsync(user, request.Password!, lockoutOnFailure: false);
        if (!check.Succeeded)
            return InvalidCredentials();

        await signInManager.SignInAsync(user, isPersistent: false);
        return Results.Ok(ToUserResponse(user));
    }

    private static async Task<IResult> GetCurrentUserAsync(ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        return user is null ? Results.Unauthorized() : Results.Ok(ToUserResponse(user));
    }

    // Same response for an unknown email and a wrong password, so registered emails are not revealed.
    private static IResult InvalidCredentials() =>
        Results.Problem(title: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);

    private static object ToUserResponse(ApplicationUser user) =>
        new { user.Id, user.Email, user.FirstName, user.LastName };

    private static async Task<IResult> RegisterAsync(RegisterRequest request, UserManager<ApplicationUser> userManager)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var email = request.Email!.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = request.FirstName!.Trim(),
            LastName = request.LastName!.Trim(),
            PhoneNumber = request.PhoneNumber
        };

        IdentityResult result;
        try
        {
            result = await userManager.CreateAsync(user, request.Password!);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Another request registered the same email between the uniqueness check and the insert.
            return DuplicateEmail();
        }

        if (!result.Succeeded)
            return Results.ValidationProblem(MapIdentityErrors(result.Errors));

        return Results.Json(
            new { user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber },
            statusCode: StatusCodes.Status201Created);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    private static IResult DuplicateEmail() =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Email is already in use."] });

    private static Dictionary<string, string[]> MapIdentityErrors(IEnumerable<IdentityError> identityErrors)
    {
        var errors = identityErrors.ToList();
        var result = new Dictionary<string, string[]>();

        if (errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
            result["email"] = ["Email is already in use."];

        var passwordErrors = errors.Where(e => e.Code.StartsWith("Password")).Select(e => e.Description).ToArray();
        if (passwordErrors.Length > 0)
            result["password"] = passwordErrors;

        var other = errors
            .Where(e => e.Code is not ("DuplicateEmail" or "DuplicateUserName") && !e.Code.StartsWith("Password"))
            .Select(e => e.Description)
            .ToArray();
        if (other.Length > 0)
            result["registration"] = other;

        return result;
    }
}
