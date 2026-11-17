namespace ParkingRealm.Data;

public class ParkingSpace
{
    public int Id { get; set; }
    public int Row { get; set; }
    public int Column { get; set; }
    public bool IsAccessible { get; set; }
    public bool HasCharger { get; set; }
}
