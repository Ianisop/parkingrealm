using Microsoft.AspNetCore.Identity;
using ParkingRealm.Data;

namespace ParkingRealm.Endpoints;

public static class PasswordResetEndpoints
{
    public static void MapPasswordResetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/forgot-password", ForgotPasswordAsync);
        app.MapPost("/api/auth/reset-password", ResetPasswordAsync);
    }

    // Same response whether or not the e-mail belongs to an account, so registered emails are not revealed.
    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        UserManager<ApplicationUser> userManager,
        PasswordResetService resetService,
        CancellationToken ct)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var user = await userManager.FindByEmailAsync(request.Email!.Trim());
        if (user is { DeletedAt: null })
            await resetService.IssueAsync(user, ct);

        return Results.Ok(new { message = "If the email belongs to an account, a reset link has been sent." });
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request, UserManager<ApplicationUser> userManager)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var user = await userManager.FindByEmailAsync(request.Email!.Trim());
        if (user is null || user.DeletedAt is not null || !PasswordResetService.TryDecodeToken(request.Token!, out var token))
            return InvalidLink();

        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword!);
        if (result.Succeeded)
            return Results.NoContent();

        // The token is checked before the password, so a bad token never reveals password rule feedback.
        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
            return InvalidLink();

        return Results.ValidationProblem(UserValidation.MapIdentityErrors(result.Errors));
    }

    private static IResult InvalidLink() =>
        Results.Problem(title: "The reset link is invalid or has expired.", statusCode: StatusCodes.Status400BadRequest);
}

public record ForgotPasswordRequest(string? Email)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        UserValidation.ValidateEmail(errors, Email);
        return errors;
    }
}

public record ResetPasswordRequest(string? Email, string? Token, string? NewPassword)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        UserValidation.ValidateEmail(errors, Email);
        if (string.IsNullOrWhiteSpace(Token))
            errors["token"] = ["Token is required."];
        if (string.IsNullOrEmpty(NewPassword))
            errors["password"] = ["Password is required."];
        return errors;
    }
}
