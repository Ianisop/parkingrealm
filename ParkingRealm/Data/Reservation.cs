namespace ParkingRealm.Data;

public enum ReservationStatus
{
    Active,
    Cancelled
}

public class Reservation
{
    public int Id { get; set; }
    public required string OwnerId { get; set; }
    public ApplicationUser Owner { get; set; } = null!;
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public int ParkingSpaceId { get; set; }
    public ParkingSpace ParkingSpace { get; set; } = null!;
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}
