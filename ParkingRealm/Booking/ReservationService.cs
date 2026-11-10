using Microsoft.EntityFrameworkCore;
using ParkingRealm.Data;

namespace ParkingRealm.Booking;

public enum ReservationOutcome
{
    Created,
    VehicleNotFound,
    Unavailable
}

public record ReservationCreation(ReservationOutcome Outcome, Reservation? Reservation = null);

public enum CancellationOutcome
{
    Cancelled,
    NotFound,
    AlreadyCancelled
}

public class ReservationService(ApplicationDbContext db)
{
    public async Task<CancellationOutcome> CancelAsync(string ownerId, int id, CancellationToken ct = default)
    {
        var spaceId = await db.Reservations.AsNoTracking().Where(r => r.Id == id && r.OwnerId == ownerId)
            .Select(r => (int?)r.ParkingSpaceId).SingleOrDefaultAsync(ct);
        if (spaceId is null)
            return CancellationOutcome.NotFound;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.ParkingSpaces.FromSqlInterpolated(
            $"SELECT * FROM [ParkingSpaces] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {spaceId.Value}")
            .AsNoTracking().SingleAsync(ct);
        var reservation = await db.Reservations.SingleOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId, ct);
        if (reservation is null)
            return CancellationOutcome.NotFound;
        if (reservation.Status == ReservationStatus.Cancelled)
            return CancellationOutcome.AlreadyCancelled;

        reservation.Status = ReservationStatus.Cancelled;
        reservation.CancelledAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return CancellationOutcome.Cancelled;
    }

    public async Task<ReservationCreation> CreateAsync(
        string ownerId, int vehicleId, BookingInterval interval, int? parkingSpaceId, CancellationToken ct = default)
    {
        if (interval.Start <= DateTimeOffset.UtcNow || interval.End <= interval.Start)
            throw new ArgumentException("A future, positive booking interval is required.", nameof(interval));
        if (!await db.Vehicles.AnyAsync(v => v.Id == vehicleId && v.OwnerId == ownerId, ct))
            return new(ReservationOutcome.VehicleNotFound);

        var candidates = db.ParkingSpaces.AsNoTracking().AsQueryable();
        if (parkingSpaceId.HasValue)
            candidates = candidates.Where(s => s.Id == parkingSpaceId.Value);
        var ids = await candidates.OrderBy(s => s.Id).Select(s => s.Id).ToListAsync(ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var id in ids)
        {
            // Every reservation writer locks this stable row before checking overlaps.
            var space = await db.ParkingSpaces.FromSqlInterpolated(
                $"SELECT * FROM [ParkingSpaces] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
                .AsNoTracking().SingleOrDefaultAsync(ct);
            if (space is null || await db.Reservations.AnyAsync(r =>
                    r.ParkingSpaceId == id && r.Status == ReservationStatus.Active &&
                    r.Start < interval.End && interval.Start < r.End, ct))
                continue;

            var reservation = new Reservation
            {
                OwnerId = ownerId,
                VehicleId = vehicleId,
                ParkingSpaceId = id,
                Start = interval.Start.ToUniversalTime(),
                End = interval.End.ToUniversalTime(),
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Reservations.Add(reservation);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(ReservationOutcome.Created, reservation);
        }
        return new(ReservationOutcome.Unavailable);
    }
}
