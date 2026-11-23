namespace ParkingRealm.Data;

public class OccupancyObservation
{
    public int Id { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public int Capacity { get; set; }
    public int Occupied { get; set; }
}

public class AttendanceObservation
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;
    public bool Attended { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}
