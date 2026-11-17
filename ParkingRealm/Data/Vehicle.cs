namespace ParkingRealm.Data;

public class Vehicle
{
    public int Id { get; set; }
    public required string OwnerId { get; set; }
    public ApplicationUser Owner { get; set; } = null!;
    public required string RegistrationNumber { get; set; }
    public required string NormalizedRegistrationNumber { get; set; }
}
