using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ParkingRealm.Data;

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
    }

    private static async Task<IResult> RegisterAsync(
        VehicleRequest request, ClaimsPrincipal principal, ApplicationDbContext db, CancellationToken ct)
    {
        var number = request.RegistrationNumber?.Trim();
        if (string.IsNullOrWhiteSpace(number) || number.Length > 32)
            return InvalidNumber("Registration number must contain 1 to 32 characters.");

        var ownerId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var normalized = number.ToUpperInvariant();
        if (await db.Vehicles.AnyAsync(v => v.OwnerId == ownerId && v.NormalizedRegistrationNumber == normalized, ct))
            return InvalidNumber("Vehicle is already registered.");

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
