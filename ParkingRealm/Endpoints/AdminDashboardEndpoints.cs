using Microsoft.EntityFrameworkCore;
using ParkingRealm.Booking;
using ParkingRealm.Data;

namespace ParkingRealm.Endpoints;

public static class AdminDashboardEndpoints
{
    public static void MapAdminDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/dashboard").RequireAuthorization("Administrator");
        group.MapGet("", GetAsync);
        group.MapPost("/occupancy", RecordOccupancyAsync).AddEndpointFilter<BookingAntiforgeryFilter>();
        group.MapPost("/attendance", RecordAttendanceAsync).AddEndpointFilter<BookingAntiforgeryFilter>();
    }

    private static async Task<IResult> GetAsync(string? start, string? end, HttpResponse response,
        ApplicationDbContext db, CancellationToken ct)
    {
        response.Headers.CacheControl = "no-store";
        var now = DateTimeOffset.UtcNow;
        var errors = BookingInterval.Validate(start, end, now, false, out var interval);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (interval.End - interval.Start > TimeSpan.FromDays(366))
            return Results.Problem(statusCode: 400, title: "Select at most 366 days.");
        var capacity = await db.ParkingSpaces.CountAsync(ct);
        var reserved = await db.Reservations.CountAsync(r => r.Status == ReservationStatus.Active && r.Start <= now && now < r.End, ct);
        var latest = await db.OccupancyObservations.AsNoTracking().OrderByDescending(o => o.RecordedAt).ThenByDescending(o => o.Id).FirstOrDefaultAsync(ct);
        var fresh = latest is not null && now - latest.RecordedAt <= TimeSpan.FromMinutes(5) && latest.Capacity == capacity;
        var history = await db.OccupancyObservations.AsNoTracking()
            .Where(o => o.RecordedAt >= interval.Start && o.RecordedAt < interval.End).OrderBy(o => o.RecordedAt).ToListAsync(ct);
        var bookings = await db.Reservations.AsNoTracking()
            .Where(r => r.Start >= interval.Start && r.Start < interval.End).ToListAsync(ct);
        var completed = bookings.Where(r => r.Status == ReservationStatus.Active && r.End <= now).Select(r => r.Id).ToHashSet();
        var attendance = await db.AttendanceObservations.AsNoTracking()
            .Where(o => completed.Contains(o.ReservationId)).ToListAsync(ct);
        var attended = attendance.Count(o => o.Attended);
        var noShows = attendance.Count - attended;
        var pending = await db.Reservations.AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Active && r.End <= now &&
                !db.AttendanceObservations.Any(o => o.ReservationId == r.Id))
            .OrderByDescending(r => r.End).Take(100)
            .Select(r => new { r.Id, r.ParkingSpaceId, r.Start, r.End }).ToListAsync(ct);
        var today = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var todayBookings = await db.Reservations.CountAsync(r => r.Status == ReservationStatus.Active && r.Start >= today && r.Start < today.AddDays(1), ct);
        return Results.Ok(new
        {
            GeneratedAt = now,
            Status = new { Capacity = capacity, Reserved = reserved, HasData = fresh,
                Occupied = fresh ? (int?)latest!.Occupied : null,
                Available = fresh ? (int?)(capacity - latest!.Occupied) : null,
                ObservedAt = latest?.RecordedAt },
            Occupancy = new { HasData = history.Count > 0,
                Days = history.GroupBy(o => o.RecordedAt.UtcDateTime.Date).Select(g => new
                {
                    Day = g.Key, Samples = g.Count(),
                    AveragePercent = g.Average(o => 100.0 * o.Occupied / o.Capacity)
                }) },
            Bookings = new { HasData = bookings.Count > 0, Total = bookings.Count,
                Active = bookings.Count(r => r.Status == ReservationStatus.Active),
                Cancelled = bookings.Count(r => r.Status == ReservationStatus.Cancelled),
                Days = bookings.GroupBy(r => r.Start.UtcDateTime.Date).OrderBy(g => g.Key)
                    .Select(g => new { Day = g.Key, Count = g.Count() }),
                Hours = bookings.GroupBy(r => r.Start.UtcDateTime.Hour).OrderBy(g => g.Key)
                    .Select(g => new { Hour = g.Key, Count = g.Count() }) },
            Attendance = new { HasData = attendance.Count > 0, Eligible = completed.Count,
                Attended = attended, NoShows = noShows, Unknown = completed.Count - attendance.Count,
                Percent = attendance.Count > 0 ? (double?)(100.0 * attended / attendance.Count) : null,
                Pending = pending },
            Revenue = new { IsDemo = true, Currency = "DKK", Day = today,
                ActualExample = 1250m, ExpectedExample = 1800m,
                Method = "Fictional daily example amounts; not measured revenue. No payment integration.",
                TodayBookings = todayBookings }
        });
    }

    private static async Task<IResult> RecordOccupancyAsync(OccupancyRequest request, ApplicationDbContext db, CancellationToken ct)
    {
        var capacity = await db.ParkingSpaces.CountAsync(ct);
        if (capacity <= 0 || request.Occupied is null || request.Occupied < 0 || request.Occupied > capacity)
            return Results.Problem(statusCode: 400, title: "Occupied count must be between zero and the current capacity.");
        db.OccupancyObservations.Add(new() { RecordedAt = DateTimeOffset.UtcNow, Capacity = capacity, Occupied = request.Occupied.Value });
        await db.SaveChangesAsync(ct);
        return Results.Ok();
    }

    private static async Task<IResult> RecordAttendanceAsync(AttendanceRequest request, ApplicationDbContext db, CancellationToken ct)
    {
        if (request.Attended is null) return Results.Problem(statusCode: 400, title: "Choose attended or no-show explicitly.");
        var reservation = await db.Reservations.SingleOrDefaultAsync(r => r.Id == request.ReservationId, ct);
        if (reservation is null) return Results.NotFound();
        if (reservation.Status != ReservationStatus.Active || reservation.End > DateTimeOffset.UtcNow)
            return Results.Problem(statusCode: 400, title: "Only completed, non-cancelled reservations can have attendance recorded.");
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var existing = await db.AttendanceObservations.SingleOrDefaultAsync(o => o.ReservationId == request.ReservationId, ct);
        if (existing is not null) return Results.Conflict();
        db.AttendanceObservations.Add(new() { ReservationId = request.ReservationId, Attended = request.Attended.Value, RecordedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Ok();
    }

    public record OccupancyRequest(int? Occupied);
    public record AttendanceRequest(int ReservationId, bool? Attended);
}
