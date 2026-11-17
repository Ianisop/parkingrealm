using Microsoft.EntityFrameworkCore;
using ParkingRealm.Booking;
using ParkingRealm.Data;

namespace ParkingRealm.Endpoints;

public static class BookingStatisticsEndpoints
{
    public static void MapBookingStatisticsEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/admin/booking-statistics", GetStatisticsAsync).RequireAuthorization("Administrator");

    private static async Task<IResult> GetStatisticsAsync(
        string? start, string? end, HttpResponse response, ApplicationDbContext db, CancellationToken ct)
    {
        response.Headers.CacheControl = "no-store";
        var errors = BookingInterval.Validate(start, end, DateTimeOffset.UtcNow, false, out var interval);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var days = await db.Reservations.AsNoTracking()
            .Where(r => interval.Start <= r.Start && r.Start < interval.End)
            .GroupBy(r => r.Start.Date)
            .OrderBy(group => group.Key)
            .Select(group => new DailyBookingCounts(group.Key, group.Count(),
                group.Count(r => r.Status == ReservationStatus.Active),
                group.Count(r => r.Status == ReservationStatus.Cancelled)))
            .ToListAsync(ct);
        var total = days.Sum(day => day.Total);
        return Results.Ok(new
        {
            Total = total,
            Active = days.Sum(day => day.Active),
            Cancelled = days.Sum(day => day.Cancelled),
            HasData = total > 0,
            Days = days
        });
    }
}

public record DailyBookingCounts(DateTime Day, int Total, int Active, int Cancelled);
