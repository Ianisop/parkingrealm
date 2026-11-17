using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ParkingRealm.Data;
using ParkingRealm.Registry;

namespace ParkingRealm.Endpoints;

public static class VehicleEndpoints
{
    public static void MapVehicleEndpoints(this IEndpointRouteBuilder app)
    {
        var vehicles = app.MapGroup("/api/vehicles").RequireAuthorization();
        vehicles.MapGet("", async (ClaimsPrincipal principal, ApplicationDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Vehicles.AsNoTracking()
                .Where(v => v.OwnerId == principal.FindFirstValue(ClaimTypes.NameIdentifier))
                .OrderBy(v => v.Id).Select(v => new { v.Id, v.RegistrationNumber }).ToListAsync(ct)));
        vehicles.MapPost("", RegisterAsync).AddEndpointFilter<BookingAntiforgeryFilter>();
        vehicles.MapDelete("/{id:int}", DeleteAsync).AddEndpointFilter<BookingAntiforgeryFilter>();
    }

    private static async Task<IResult> DeleteAsync(
        int id, ClaimsPrincipal principal, ApplicationDbContext db, CancellationToken ct)
    {
        var ownerId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == id && v.OwnerId == ownerId, ct);
        if (vehicle is null)
            return Results.NotFound();
        if (await db.Reservations.AnyAsync(r => r.VehicleId == id, ct))
            return InUse();

        db.Vehicles.Remove(vehicle);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            // A reservation was committed after the check above; the restrictive foreign key rejected the delete.
            db.ChangeTracker.Clear();
            if (await db.Reservations.AnyAsync(r => r.VehicleId == id, ct))
                return InUse();
            throw;
        }
        return Results.NoContent();
    }

    private static IResult InUse() => Results.Problem(
        title: "The vehicle has reservations and cannot be deleted.", statusCode: StatusCodes.Status409Conflict);

    private static async Task<IResult> RegisterAsync(
        VehicleRequest request, ClaimsPrincipal principal, ApplicationDbContext db, IVehicleRegistry registry,
        CancellationToken ct)
    {
        var number = request.RegistrationNumber?.Trim();
        if (string.IsNullOrWhiteSpace(number) || number.Length > 32)
            return InvalidNumber("Registration number must contain 1 to 32 characters.");

        var ownerId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var normalized = number.ToUpperInvariant();
        if (await db.Vehicles.AnyAsync(v => v.OwnerId == ownerId && v.NormalizedRegistrationNumber == normalized, ct))
            return InvalidNumber("Vehicle is already registered.");

        switch (await registry.LookupAsync(normalized, ct))
        {
            case RegistryOutcome.NotFound:
                return InvalidNumber("The registration number was not found in the vehicle registry.");
            case RegistryOutcome.Unavailable:
                return Results.Problem(title: "The vehicle registry is unavailable. Try again later.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var vehicle = new Vehicle
        {
            OwnerId = ownerId,
            RegistrationNumber = number,
            NormalizedRegistrationNumber = normalized
        };
        db.Vehicles.Add(vehicle);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return InvalidNumber("Vehicle is already registered.");
        }
        return Results.Json(new { vehicle.Id, vehicle.RegistrationNumber }, statusCode: StatusCodes.Status201Created);
    }

    private static IResult InvalidNumber(string message) => Results.ValidationProblem(
        new Dictionary<string, string[]> { ["registrationNumber"] = [message] });
}

public record VehicleRequest(string? RegistrationNumber);
