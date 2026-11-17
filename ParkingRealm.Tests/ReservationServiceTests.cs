using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingRealm.Booking;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class ReservationServiceTests(ApiFactory factory)
{
    private async Task<ReservationCreation> CreateAsync(ReservationTestData data, BookingInterval interval, int? spaceId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ReservationService>()
            .CreateAsync(data.OwnerId, data.VehicleId, interval, spaceId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_requests_commit_only_one_overlap(bool automatic)
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        var interval = new BookingInterval(data.Start, data.Start.AddHours(1));
        int? selected = automatic ? null : data.SpaceId;
        var results = await Task.WhenAll(
            Task.Run(() => CreateAsync(data, interval, selected)),
            Task.Run(() => CreateAsync(data, interval, selected)));

        Assert.Single(results, r => r.Outcome == ReservationOutcome.Created);
        Assert.Single(results, r => r.Outcome == ReservationOutcome.Unavailable);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.Reservations.CountAsync(r => r.ParkingSpaceId == data.SpaceId));
    }

    [Fact]
    public async Task Adjacent_intervals_are_both_committed()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        var first = new BookingInterval(data.Start, data.Start.AddHours(1));
        var next = new BookingInterval(first.End, first.End.AddHours(1));
        Assert.Equal(ReservationOutcome.Created, (await CreateAsync(data, first, data.SpaceId)).Outcome);
        Assert.Equal(ReservationOutcome.Created, (await CreateAsync(data, next, data.SpaceId)).Outcome);
    }

    [Fact]
    public async Task Another_owner_cannot_book_a_vehicle()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ReservationService>()
            .CreateAsync("another-owner", data.VehicleId, new(data.Start, data.Start.AddHours(1)), data.SpaceId);
        Assert.Equal(ReservationOutcome.VehicleNotFound, result.Outcome);
    }
}
