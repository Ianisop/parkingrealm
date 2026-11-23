using ParkingRealm.Booking;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

public class ParkingSpaceRankerTests
{
    [Fact]
    public void Nearest_location_wins_and_equal_distances_use_lowest_id()
    {
        var spaces = new[]
        {
            new ParkingSpace { Id = 1, Row = 10, Column = 10 },
            new ParkingSpace { Id = 5, Row = 2, Column = 3 },
            new ParkingSpace { Id = 3, Row = 3, Column = 2 },
            new ParkingSpace { Id = 9, Row = 2, Column = 2 }
        };

        Assert.Equal(new[] { 9, 3, 5, 1 }, ParkingSpaceRanker.Rank(spaces, spaces[3]).Select(s => s.Id));
    }

    [Fact]
    public void No_preference_preserves_lowest_id_order_and_empty_inventory_is_supported()
    {
        var spaces = new[] { new ParkingSpace { Id = 8 }, new ParkingSpace { Id = 2 } };
        Assert.Equal(new[] { 2, 8 }, ParkingSpaceRanker.Rank(spaces).Select(s => s.Id));
        Assert.Empty(ParkingSpaceRanker.Rank([]));
    }

    [Fact]
    public void Reference_does_not_have_to_be_an_available_candidate()
    {
        var reference = new ParkingSpace { Id = 7, Row = 5, Column = 5 };
        var spaces = new[]
        {
            new ParkingSpace { Id = 1, Row = 0, Column = 0 },
            new ParkingSpace { Id = 2, Row = 5, Column = 6 }
        };
        Assert.Equal(2, ParkingSpaceRanker.Rank(spaces, reference).First().Id);
    }

    [Fact]
    public void Distance_does_not_overflow_for_large_coordinates()
    {
        var space = new ParkingSpace { Row = int.MaxValue, Column = int.MaxValue };
        var reference = new ParkingSpace { Row = int.MinValue, Column = int.MinValue };
        Assert.Equal(8589934590L, ParkingSpaceRanker.GridDistance(space, reference));
    }

    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 3)]
    [InlineData(true, true, 4)]
    public void Requirements_are_filtered_before_distance_ranking(bool accessible, bool charger, int expectedId)
    {
        var spaces = new[]
        {
            new ParkingSpace { Id = 1, Column = 0 },
            new ParkingSpace { Id = 2, Column = 1, IsAccessible = true },
            new ParkingSpace { Id = 3, Column = 2, HasCharger = true },
            new ParkingSpace { Id = 4, Column = 3, IsAccessible = true, HasCharger = true }
        };
        var requirements = new ParkingRequirements(accessible, charger, IsElectricVehicle: charger);
        Assert.Equal(expectedId, Assert.Single(ParkingSpaceRanker.Rank(spaces, spaces[0], requirements)).Id);
    }

    [Fact]
    public void Both_features_must_exist_on_the_same_space()
    {
        var spaces = new[] { new ParkingSpace { IsAccessible = true }, new ParkingSpace { HasCharger = true } };
        Assert.Empty(ParkingSpaceRanker.Rank(spaces, requirements: new(true, true, true)));
    }

    [Fact]
    public void Electric_vehicles_can_use_chargers_without_a_charging_requirement()
    {
        var spaces = new[] { new ParkingSpace { Id = 1 }, new ParkingSpace { Id = 2, HasCharger = true } };
        Assert.Equal(new[] { 1, 2 }, ParkingSpaceRanker.Rank(spaces, requirements: new(false, false, true)).Select(s => s.Id));
        Assert.Equal(2, Assert.Single(ParkingSpaceRanker.Rank(spaces, requirements: new(false, true, true))).Id);
        Assert.Equal(1, Assert.Single(ParkingSpaceRanker.Rank(spaces)).Id);
        Assert.Empty(ParkingSpaceRanker.Rank(spaces, requirements: new(false, true, false)));
    }
}
