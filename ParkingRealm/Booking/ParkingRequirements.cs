using ParkingRealm.Data;

namespace ParkingRealm.Booking;

public readonly record struct ParkingRequirements(
    bool RequiresAccessible, bool RequiresCharger, bool IsElectricVehicle = false)
{
    public bool Matches(ParkingSpace space) =>
        space.IsAccessible == RequiresAccessible &&
        (!RequiresCharger || space.HasCharger) && (!space.HasCharger || IsElectricVehicle);
}
