using ParkingRealm.Data;

namespace ParkingRealm.Booking;

public static class ParkingSpaceRanker
{
    public static long GridDistance(ParkingSpace space, ParkingSpace reference) =>
        Math.Abs((long)space.Row - reference.Row) + Math.Abs((long)space.Column - reference.Column);

    public static IOrderedEnumerable<ParkingSpace> Rank(
        IEnumerable<ParkingSpace> spaces, ParkingSpace? preferredSpace = null,
        ParkingRequirements requirements = default) =>
        spaces.Where(requirements.Matches)
            .OrderBy(space => preferredSpace is null ? 0L : GridDistance(space, preferredSpace))
            .ThenBy(space => space.Id);
}
