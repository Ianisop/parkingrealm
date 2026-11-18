using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ParkingRealm.Data;

namespace ParkingRealm.Endpoints;

public static class UserAdministrationEndpoints
{
    private const int MaxListedUsers = 200;

    public static void MapUserAdministrationEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/admin/users").RequireAuthorization("Administrator");
        // Responses contain personal data and must never be cached.
        users.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        users.MapGet("", ListAsync);
        users.MapPost("", CreateAsync).AddEndpointFilter<BookingAntiforgeryFilter>();
        users.MapPut("/{id}", UpdateAsync).AddEndpointFilter<BookingAntiforgeryFilter>();
        users.MapDelete("/{id}", DeleteAsync).AddEndpointFilter<BookingAntiforgeryFilter>();
    }

    private static async Task<IResult> ListAsync(string? search, ApplicationDbContext db, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking().Where(u => u.DeletedAt == null);
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
            query = query.Where(u => u.Email!.Contains(term) || u.FirstName.Contains(term) || u.LastName.Contains(term));

        var administratorIds = from userRole in db.UserRoles
                               join role in db.Roles on userRole.RoleId equals role.Id
                               where role.Name == AdministratorProvisioning.RoleName
                               select userRole.UserId;
        var result = await query
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Email)
            .Take(MaxListedUsers)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.PhoneNumber,
                IsAdministrator = administratorIds.Contains(u.Id)
            })
            .ToListAsync(ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateAsync(RegisterRequest request, UserManager<ApplicationUser> userManager)
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
            return UserValidation.DuplicateEmail();
        }

        if (!result.Succeeded)
            return Results.ValidationProblem(UserValidation.MapIdentityErrors(result.Errors));

        return Results.Json(ToResponse(user, isAdministrator: false), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateAsync(
        string id, UpdateUserRequest request, UserManager<ApplicationUser> userManager)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var user = await FindActiveAsync(userManager, id);
        if (user is null)
            return Results.NotFound();
        if (await userManager.IsInRoleAsync(user, AdministratorProvisioning.RoleName))
            return AdministratorProtected();

        var email = request.Email!.Trim();
        user.UserName = email;
        user.Email = email;
        user.FirstName = request.FirstName!.Trim();
        user.LastName = request.LastName!.Trim();
        user.PhoneNumber = request.PhoneNumber;

        IdentityResult result;
        try
        {
            result = await userManager.UpdateAsync(user);
        }
        catch (DbUpdateException ex) when (UserValidation.IsUniqueViolation(ex))
        {
            return UserValidation.DuplicateEmail();
        }

        if (!result.Succeeded)
            return Results.ValidationProblem(UserValidation.MapIdentityErrors(result.Errors));

        return Results.Ok(ToResponse(user, isAdministrator: false));
    }

    private static async Task<IResult> DeleteAsync(
        string id, UserManager<ApplicationUser> userManager, ApplicationDbContext db, CancellationToken ct)
    {
        var user = await FindActiveAsync(userManager, id);
        if (user is null)
            return Results.NotFound();
        if (await userManager.IsInRoleAsync(user, AdministratorProvisioning.RoleName))
            return AdministratorProtected();

        if (!await db.Reservations.AnyAsync(r => r.OwnerId == id, ct))
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await db.Vehicles.Where(v => v.OwnerId == id).ExecuteDeleteAsync(ct);
                var deleted = await userManager.DeleteAsync(user);
                if (!deleted.Succeeded)
                    return Results.Problem(title: "The user could not be deleted.", statusCode: StatusCodes.Status500InternalServerError);
                await transaction.CommitAsync(ct);
                return Results.NoContent();
            }
            catch (Exception ex) when (IsForeignKeyViolation(ex))
            {
                // A reservation was committed after the check above; keep the history and anonymize instead.
                db.ChangeTracker.Clear();
                user = await FindActiveAsync(userManager, id);
                if (user is null)
                    return Results.NotFound();
            }
        }

        await AnonymizeAsync(user, userManager, db, ct);
        return Results.NoContent();
    }

    // Keeps the row so reservations and statistics survive, but removes everything that identifies the person.
    private static async Task AnonymizeAsync(
        ApplicationUser user, UserManager<ApplicationUser> userManager, ApplicationDbContext db, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        foreach (var vehicle in await db.Vehicles.Where(v => v.OwnerId == user.Id).ToListAsync(ct))
        {
            vehicle.RegistrationNumber = $"DELETED-{vehicle.Id}";
            vehicle.NormalizedRegistrationNumber = vehicle.RegistrationNumber;
        }

        var placeholder = $"deleted-{Guid.NewGuid():N}@deleted.invalid";
        user.UserName = placeholder;
        user.Email = placeholder;
        user.EmailConfirmed = false;
        user.FirstName = "Slettet";
        user.LastName = "Bruger";
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.PasswordHash = null;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.DeletedAt = DateTimeOffset.UtcNow;

        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded)
            throw new InvalidOperationException("The user could not be anonymized.");

        await db.UserRoles.Where(r => r.UserId == user.Id).ExecuteDeleteAsync(ct);
        await db.UserClaims.Where(c => c.UserId == user.Id).ExecuteDeleteAsync(ct);
        await db.UserLogins.Where(l => l.UserId == user.Id).ExecuteDeleteAsync(ct);
        await db.UserTokens.Where(t => t.UserId == user.Id).ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }

    private static async Task<ApplicationUser?> FindActiveAsync(UserManager<ApplicationUser> userManager, string id)
    {
        var user = await userManager.FindByIdAsync(id);
        return user is { DeletedAt: null } ? user : null;
    }

    private static bool IsForeignKeyViolation(Exception ex) =>
        ex is SqlException { Number: 547 } || ex.InnerException is SqlException { Number: 547 };

    private static IResult AdministratorProtected() => Results.Problem(
        title: "Administrator accounts cannot be changed or deleted here.", statusCode: StatusCodes.Status403Forbidden);

    private static object ToResponse(ApplicationUser user, bool isAdministrator) =>
        new { user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber, IsAdministrator = isAdministrator };
}

public record UpdateUserRequest(string? Email, string? FirstName, string? LastName, string? PhoneNumber)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        UserValidation.ValidateEmail(errors, Email);
        UserValidation.ValidateName(errors, "firstName", "First name", FirstName);
        UserValidation.ValidateName(errors, "lastName", "Last name", LastName);
        UserValidation.ValidatePhoneNumber(errors, PhoneNumber);
        return errors;
    }
}
