using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingRealm.Booking;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class ReservationServiceTests(ApiFactory factory)
{
    private async Task<ReservationCreation> CreateAsync(
        ReservationTestData data, BookingInterval interval, int? spaceId, int? preferredSpaceId = null,
        ParkingRequirements requirements = default)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ReservationService>()
            .CreateAsync(data.OwnerId, data.VehicleId, interval, spaceId, preferredSpaceId, requirements);
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

    [Fact]
    public async Task Automatic_assignment_uses_nearest_available_location_not_lowest_id()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        var preferred = await data.AddSpaceAsync(2);
        var nearest = await data.AddSpaceAsync(1);
        var interval = new BookingInterval(data.Start, data.Start.AddHours(1));

        var first = await CreateAsync(data, interval, null, preferred);
        Assert.Equal(ReservationOutcome.Created, first.Outcome);
        Assert.Equal(preferred, first.Reservation!.ParkingSpaceId);
        var fallback = await CreateAsync(data, interval, null, preferred);
        Assert.Equal(ReservationOutcome.Created, fallback.Outcome);
        Assert.Equal(nearest, fallback.Reservation!.ParkingSpaceId);
    }

    [Fact]
    public async Task Concurrent_ranked_requests_fall_back_without_committing_overlaps()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        var preferred = await data.AddSpaceAsync(1);
        var interval = new BookingInterval(data.Start, data.Start.AddHours(1));
        var results = await Task.WhenAll(
            Task.Run(() => CreateAsync(data, interval, null, preferred)),
            Task.Run(() => CreateAsync(data, interval, null, preferred)))
            .WaitAsync(TimeSpan.FromSeconds(30));

        Assert.All(results, result => Assert.Equal(ReservationOutcome.Created, result.Outcome));
        Assert.Equal(new[] { data.SpaceId, preferred }.Order(), results.Select(r => r.Reservation!.ParkingSpaceId).Order());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Concurrent_requests_never_fall_back_to_a_space_without_required_features(bool accessible, bool charger)
    {
        await using var data = await ReservationTestData.CreateAsync(factory, isElectric: charger);
        var first = await data.AddSpaceAsync(1, accessible, charger);
        var second = await data.AddSpaceAsync(2, accessible, charger);
        var interval = new BookingInterval(data.Start, data.Start.AddHours(1));
        var requirements = new ParkingRequirements(accessible, charger);
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
            CreateAsync(data, interval, null, data.SpaceId, requirements)))).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(2, results.Count(result => result.Outcome == ReservationOutcome.Created));
        Assert.Single(results, result => result.Outcome == ReservationOutcome.Unavailable);
        Assert.Equal(new[] { first, second }.Order(),
            results.Where(result => result.Reservation is not null).Select(result => result.Reservation!.ParkingSpaceId).Order());
    }

    [Fact]
    public async Task Caller_supplied_electric_eligibility_cannot_override_the_owned_vehicle()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        var charging = await data.AddSpaceAsync(1, hasCharger: true);
        var interval = new BookingInterval(data.Start, data.Start.AddHours(1));
        var result = await CreateAsync(data, interval, charging, requirements: new(false, false, true));
        Assert.Equal(ReservationOutcome.Unavailable, result.Outcome);
    }

    [Fact]
    public async Task Opposite_preferences_release_rejected_locks_instead_of_deadlocking()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        var other = await data.AddSpaceAsync(1);
        var interval = new BookingInterval(data.Start, data.Start.AddHours(1));
        Assert.Equal(ReservationOutcome.Created, (await CreateAsync(data, interval, data.SpaceId)).Outcome);
        Assert.Equal(ReservationOutcome.Created, (await CreateAsync(data, interval, other)).Outcome);

        var results = await Task.WhenAll(
            Task.Run(() => CreateAsync(data, interval, null, data.SpaceId)),
            Task.Run(() => CreateAsync(data, interval, null, other)))
            .WaitAsync(TimeSpan.FromSeconds(30));
        Assert.All(results, result => Assert.Equal(ReservationOutcome.Unavailable, result.Outcome));
    }
}
