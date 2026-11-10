using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

public class BookingModelTests
{
    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ApiFactory.TestConnectionString).Options);

    [Fact]
    public void Spaces_and_plates_have_unique_indexes()
    {
        using var db = CreateContext();
        var space = db.Model.FindEntityType(typeof(ParkingSpace))!;
        var vehicle = db.Model.FindEntityType(typeof(Vehicle))!;

        Assert.Contains(space.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual([nameof(ParkingSpace.Row), nameof(ParkingSpace.Column)]));
        Assert.Contains(vehicle.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual([nameof(Vehicle.OwnerId), nameof(Vehicle.NormalizedRegistrationNumber)]));
    }

    [Fact]
    public void Reservation_vehicle_must_belong_to_reservation_owner()
    {
        using var db = CreateContext();
        var reservation = db.Model.FindEntityType(typeof(Reservation))!;
        var vehicleKey = reservation.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(Vehicle));

        Assert.Equal([nameof(Reservation.VehicleId), nameof(Reservation.OwnerId)], vehicleKey.Properties.Select(p => p.Name));
        Assert.Equal([nameof(Vehicle.Id), nameof(Vehicle.OwnerId)], vehicleKey.PrincipalKey.Properties.Select(p => p.Name));
        var designTimeReservation = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Reservation))!;
        Assert.Contains(designTimeReservation.GetCheckConstraints(), constraint => constraint.Name == "CK_Reservations_Interval");
    }
}
