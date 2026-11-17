using Microsoft.EntityFrameworkCore;
using ParkingRealm.Booking;
using ParkingRealm.Data;

namespace ParkingRealm.Endpoints;

public static class ParkingEndpoints
{
    public static void MapParkingEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/parking-spaces", GetAvailabilityAsync);

    private static async Task<IResult> GetAvailabilityAsync(
        string? start, string? end, HttpResponse response, ApplicationDbContext db, CancellationToken ct)
    {
        response.Headers.CacheControl = "no-store";
        var now = DateTimeOffset.UtcNow;
        var reservations = db.Reservations.Where(r => r.Status == ReservationStatus.Active);
        if (start is null && end is null)
        {
            reservations = reservations.Where(r => r.Start <= now && now < r.End);
        }
        else
        {
            var errors = BookingInterval.Validate(start, end, now, requireFuture: false, out var interval);
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);
            reservations = reservations.Where(r => r.Start < interval.End && interval.Start < r.End);
        }

        return Results.Ok(await db.ParkingSpaces.AsNoTracking().OrderBy(s => s.Row).ThenBy(s => s.Column)
            .Select(s => new
            {
                s.Id, s.Row, s.Column,
                IsAvailable = !reservations.Any(r => r.ParkingSpaceId == s.Id)
            }).ToListAsync(ct));
    }
}
