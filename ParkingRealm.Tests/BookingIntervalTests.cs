using ParkingRealm.Booking;

namespace ParkingRealm.Tests;

public class BookingIntervalTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T10:00:00Z");

    [Theory]
    [InlineData(null, null)]
    [InlineData("2026-01-01T09:00:00Z", "2026-01-01T12:00:00Z")]
    [InlineData("2026-01-01T11:00:00Z", "2026-01-01T11:00:00Z")]
    [InlineData("2026-01-01T11:00:00Z", "2026-01-01T10:00:00Z")]
    [InlineData("2026-01-01T11:00:00+25:00", "2026-01-01T12:00:00Z")]
    [InlineData("2026-01-01T11:00:00", "2026-01-01T12:00:00Z")]
    public void Invalid_booking_intervals_are_rejected(string? start, string? end)
    {
        Assert.NotEmpty(BookingInterval.Validate(start, end, Now, requireFuture: true, out _));
    }

    [Fact]
    public void Valid_offsets_are_normalized_and_adjacent_ranges_are_valid()
    {
        Assert.Empty(BookingInterval.Validate("2026-01-01T13:00:00+01:00", "2026-01-01T14:00:00+01:00",
            Now, true, out var first));
        Assert.Empty(BookingInterval.Validate("2026-01-01T13:00:00Z", "2026-01-01T14:00:00Z",
            Now, true, out var second));
        Assert.Equal(TimeSpan.Zero, first.Start.Offset);
        Assert.Equal(first.End, second.Start);
    }

    [Fact]
    public void Reporting_can_use_past_intervals()
    {
        Assert.Empty(BookingInterval.Validate("2025-01-01T00:00:00Z", "2025-01-02T00:00:00Z",
            Now, requireFuture: false, out _));
    }
}
