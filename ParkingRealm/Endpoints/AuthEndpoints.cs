using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
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
        if (user is null || user.DeletedAt is not null)
            return InvalidCredentials();

        var check = await signInManager.CheckPasswordSignInAsync(user, request.Password!, lockoutOnFailure: false);
        if (!check.Succeeded)
            return InvalidCredentials();

        await signInManager.SignInAsync(user, isPersistent: false);
        return Results.Ok(ToUserResponse(user, await userManager.IsInRoleAsync(user, AdministratorProvisioning.RoleName)));
    }

    private static async Task<IResult> GetCurrentUserAsync(ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        return user is null || user.DeletedAt is not null
            ? Results.Unauthorized()
            : Results.Ok(ToUserResponse(user, principal.IsInRole(AdministratorProvisioning.RoleName)));
    }

    // Same response for an unknown email and a wrong password, so registered emails are not revealed.
    private static IResult InvalidCredentials() =>
        Results.Problem(title: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);

    private static object ToUserResponse(ApplicationUser user, bool isAdministrator) =>
        new { user.Id, user.Email, user.FirstName, user.LastName, IsAdministrator = isAdministrator };

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
        catch (DbUpdateException ex) when (UserValidation.IsUniqueViolation(ex))
        {
            // Another request registered the same email between the uniqueness check and the insert.
            return UserValidation.DuplicateEmail();
        }

        if (!result.Succeeded)
            return Results.ValidationProblem(UserValidation.MapIdentityErrors(result.Errors));

        return Results.Json(
            new { user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber },
            statusCode: StatusCodes.Status201Created);
    }
}
