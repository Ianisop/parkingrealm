using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

[Collection(ApiCollection.Name)]
public class AdminDashboardTests(ApiFactory factory)
{
    private static string Path(DateTimeOffset start, DateTimeOffset end) =>
        $"/api/admin/dashboard?start={Uri.EscapeDataString(start.ToString("O"))}&end={Uri.EscapeDataString(end.ToString("O"))}";

    [Fact]
    public async Task Reports_and_writes_require_administrator_and_csrf()
    {
        using var anonymous = factory.CreateClient();
        using var ordinary = await BookingCustomer.LoginAsync(factory);
        var path = Path(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync("/api/admin/dashboard/occupancy", new { occupied = 0 })).StatusCode);
        using var admin = await AdministratorRoleTests.LoginAdministratorAsync(factory);
        admin.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/dashboard/occupancy", new { occupied = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(Path(DateTimeOffset.UtcNow.AddDays(-400), DateTimeOffset.UtcNow))).StatusCode);
    }

    [Fact]
    public async Task Physical_status_and_retained_history_use_observations_not_reservations()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        using var admin = await AdministratorRoleTests.LoginAdministratorAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.OccupancyObservations.ExecuteDeleteAsync();
        var path = Path(DateTimeOffset.UtcNow.AddDays(-365).AddHours(-1), DateTimeOffset.UtcNow.AddHours(1));
        var empty = await admin.GetFromJsonAsync<JsonElement>(path);
        Assert.False(empty.GetProperty("status").GetProperty("hasData").GetBoolean());
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("status").GetProperty("occupied").ValueKind);
        Assert.True(empty.GetProperty("revenue").GetProperty("isDemo").GetBoolean());
        Assert.False(empty.GetProperty("occupancy").GetProperty("hasData").GetBoolean());
        var historical = new OccupancyObservation { Capacity = 10, Occupied = 5, RecordedAt = DateTimeOffset.UtcNow.AddDays(-365) };
        db.OccupancyObservations.Add(historical);
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/dashboard/occupancy", new { occupied = -1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/dashboard/occupancy", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/admin/dashboard/occupancy", new { occupied = 0 })).StatusCode);
        var result = await admin.GetFromJsonAsync<JsonElement>(path);
        Assert.True(result.GetProperty("status").GetProperty("hasData").GetBoolean());
        Assert.Equal(0, result.GetProperty("status").GetProperty("occupied").GetInt32());
        Assert.Contains(result.GetProperty("occupancy").GetProperty("days").EnumerateArray(), d => d.GetProperty("averagePercent").GetDouble() == 50);
        await db.OccupancyObservations.ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Missing_attendance_is_unknown_and_explicit_outcomes_are_counted()
    {
        await using var data = await ReservationTestData.CreateAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var start = DateTimeOffset.UtcNow.AddDays(-10);
        var reservation = new Reservation { OwnerId = data.OwnerId, VehicleId = data.VehicleId, ParkingSpaceId = data.SpaceId,
            Start = start, End = start.AddHours(1), CreatedAt = start.AddDays(-1) };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();
        using var admin = await AdministratorRoleTests.LoginAdministratorAsync(factory);
        var path = Path(start.AddMinutes(-1), start.AddHours(2));
        var before = await admin.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal(1, before.GetProperty("attendance").GetProperty("unknown").GetInt32());
        Assert.Equal(0, before.GetProperty("attendance").GetProperty("noShows").GetInt32());
        Assert.False(before.GetProperty("attendance").GetProperty("hasData").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/dashboard/attendance", new { reservationId = reservation.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/admin/dashboard/attendance", new { reservationId = reservation.Id, attended = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/dashboard/attendance", new { reservationId = reservation.Id, attended = false })).StatusCode);
        var after = await admin.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal(1, after.GetProperty("attendance").GetProperty("attended").GetInt32());
        Assert.Equal(0, after.GetProperty("attendance").GetProperty("unknown").GetInt32());
        Assert.Equal(100, after.GetProperty("attendance").GetProperty("percent").GetDouble());
        await db.AttendanceObservations.Where(o => o.ReservationId == reservation.Id).ExecuteDeleteAsync();
    }
}
