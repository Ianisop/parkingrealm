using Microsoft.EntityFrameworkCore;
using ParkingRealm.Data;

namespace ParkingRealm.Booking;

public enum ReservationOutcome
{
    Created,
    VehicleNotFound,
    ChargingNotSupported,
    PreferenceNotFound,
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

    public Task<ReservationCreation> CreateAsync(
        string ownerId, int vehicleId, BookingInterval interval, int? parkingSpaceId, CancellationToken ct = default) =>
        CreateAsync(ownerId, vehicleId, interval, parkingSpaceId, null, ct);

    public Task<ReservationCreation> CreateAsync(
        string ownerId, int vehicleId, BookingInterval interval, int? parkingSpaceId,
        int? preferredSpaceId, CancellationToken ct = default) =>
        CreateAsync(ownerId, vehicleId, interval, parkingSpaceId, preferredSpaceId, default, ct);

    public async Task<ReservationCreation> CreateAsync(
        string ownerId, int vehicleId, BookingInterval interval, int? parkingSpaceId,
        int? preferredSpaceId, ParkingRequirements requirements, CancellationToken ct = default)
    {
        if (interval.Start <= DateTimeOffset.UtcNow || interval.End <= interval.Start)
            throw new ArgumentException("A future, positive booking interval is required.", nameof(interval));
        if (parkingSpaceId.HasValue && preferredSpaceId.HasValue)
            throw new ArgumentException("Location preferences require automatic assignment.", nameof(preferredSpaceId));
        var vehicle = await db.Vehicles.AsNoTracking().Where(v => v.Id == vehicleId && v.OwnerId == ownerId)
            .Select(v => new { v.IsElectric }).SingleOrDefaultAsync(ct);
        if (vehicle is null)
            return new(ReservationOutcome.VehicleNotFound);
        requirements = requirements with { IsElectricVehicle = vehicle.IsElectric };
        if (requirements.RequiresCharger && !vehicle.IsElectric)
            return new(ReservationOutcome.ChargingNotSupported);

        var candidates = db.ParkingSpaces.AsNoTracking().AsQueryable();
        if (parkingSpaceId.HasValue)
            candidates = candidates.Where(s => s.Id == parkingSpaceId.Value);
        var spaces = await candidates.ToListAsync(ct);
        var preferredSpace = preferredSpaceId.HasValue
            ? spaces.SingleOrDefault(s => s.Id == preferredSpaceId.Value) : null;
        if (preferredSpaceId.HasValue && preferredSpace is null)
            return new(ReservationOutcome.PreferenceNotFound);
        var ids = ParkingSpaceRanker.Rank(spaces, preferredSpace, requirements).Select(s => s.Id);

        foreach (var id in ids)
        {
            if (interval.Start <= DateTimeOffset.UtcNow)
                throw new ArgumentException("A future booking interval is required.", nameof(interval));
            // Release rejected candidates before trying another preference-ordered space.
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            // Every reservation writer locks this stable row before checking overlaps.
            var space = await db.ParkingSpaces.FromSqlInterpolated(
                $"SELECT * FROM [ParkingSpaces] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
                .AsNoTracking().SingleOrDefaultAsync(ct);
            if (space is null || !requirements.Matches(space) || await db.Reservations.AnyAsync(r =>
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
