using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ParkingRealm.Booking;
using ParkingRealm.Data;

namespace ParkingRealm.Endpoints;

public static class ReservationEndpoints
{
    public static void MapReservationEndpoints(this IEndpointRouteBuilder app)
    {
        var reservations = app.MapGroup("/api/reservations").RequireAuthorization();
        reservations.MapPost("", CreateAsync).AddEndpointFilter<BookingAntiforgeryFilter>();
        reservations.MapPost("/{id:int}/cancel", CancelAsync).AddEndpointFilter<BookingAntiforgeryFilter>();
        reservations.MapGet("", async (ClaimsPrincipal principal, ApplicationDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Reservations.AsNoTracking()
                .Where(r => r.OwnerId == principal.FindFirstValue(ClaimTypes.NameIdentifier))
                .OrderByDescending(r => r.Start)
                .Select(r => new ReservationResponse(r.Id, r.VehicleId, r.ParkingSpaceId, r.Start, r.End,
                    r.Status.ToString(), r.CreatedAt, r.CancelledAt)).ToListAsync(ct)));
    }

    private static async Task<IResult> CancelAsync(
        int id, ClaimsPrincipal principal, ReservationService service, CancellationToken ct)
    {
        var result = await service.CancelAsync(principal.FindFirstValue(ClaimTypes.NameIdentifier)!, id, ct);
        return result switch
        {
            CancellationOutcome.NotFound => Results.NotFound(),
            CancellationOutcome.AlreadyCancelled => Results.Problem(title: "Reservation is already cancelled.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.Ok(new { Id = id, Status = "Cancelled" })
        };
    }

    private static async Task<IResult> CreateAsync(
        ReservationRequest request, ClaimsPrincipal principal, ReservationService service, CancellationToken ct)
    {
        var errors = BookingInterval.Validate(request.Start, request.End, DateTimeOffset.UtcNow, true, out var interval);
        if (request.VehicleId <= 0)
            errors["vehicleId"] = ["Select a registered vehicle."];
        if (request.Mode is not ("automatic" or "manual"))
            errors["mode"] = ["Select automatic or manual assignment."];
        if (request.Mode == "manual" && request.ParkingSpaceId is not > 0)
            errors["parkingSpaceId"] = ["Select a parking space."];
        if (request.Mode == "automatic" && request.ParkingSpaceId.HasValue)
            errors["parkingSpaceId"] = ["Automatic assignment does not accept a selected space."];
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        ReservationCreation result;
        try
        {
            result = await service.CreateAsync(principal.FindFirstValue(ClaimTypes.NameIdentifier)!,
                request.VehicleId, interval, request.ParkingSpaceId, ct);
        }
        catch (ArgumentException ex) when (ex.ParamName == "interval")
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["start"] = ["Start must be in the future."] });
        }
        if (result.Outcome == ReservationOutcome.VehicleNotFound)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["vehicleId"] = ["Vehicle is not available for this account."] });
        if (result.Outcome == ReservationOutcome.Unavailable)
            return Results.Problem(title: "No suitable parking space is available.", statusCode: StatusCodes.Status409Conflict);

        var r = result.Reservation!;
        return Results.Json(new ReservationResponse(r.Id, r.VehicleId, r.ParkingSpaceId, r.Start, r.End,
            r.Status.ToString(), r.CreatedAt, r.CancelledAt), statusCode: StatusCodes.Status201Created);
    }
}

public record ReservationRequest(int VehicleId, string? Start, string? End, string? Mode, int? ParkingSpaceId);
public record ReservationResponse(int Id, int VehicleId, int ParkingSpaceId, DateTimeOffset Start, DateTimeOffset End,
    string Status, DateTimeOffset CreatedAt, DateTimeOffset? CancelledAt);
