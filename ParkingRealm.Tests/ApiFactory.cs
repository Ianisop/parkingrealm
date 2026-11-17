using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ParkingRealm.Data;
using ParkingRealm.Registry;

namespace ParkingRealm.Tests;

// Plates other than the markers are found, so tests never need the running mock service.
public sealed class FakeVehicleRegistry : IVehicleRegistry
{
    public const string UnknownPlate = "UNKNOWN1";
    public const string UnavailablePlate = "DOWN0001";

    public Task<RegistryOutcome> LookupAsync(string registrationNumber, CancellationToken ct) =>
        Task.FromResult(registrationNumber switch
        {
            UnknownPlate => RegistryOutcome.NotFound,
            UnavailablePlate => RegistryOutcome.Unavailable,
            _ => RegistryOutcome.Found
        });
}

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=ParkingRealm_Tests;Trusted_Connection=True;TrustServerCertificate=True";

    public const string UnreachableConnectionString =
        "Server=localhost,1;Database=ParkingRealm_Tests;User Id=x;Password=x;TrustServerCertificate=True;Connect Timeout=3";

    private readonly string connectionString;

    public ApiFactory() : this(TestConnectionString)
    {
    }

    internal ApiFactory(string connectionString) => this.connectionString = connectionString;
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        // Development auto-migrates at startup, which would throw before any request for an unreachable database.
        if (connectionString != TestConnectionString)
            builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IVehicleRegistry>();
            services.AddSingleton<IVehicleRegistry, FakeVehicleRegistry>();
        });
    }

    public async Task InitializeAsync()
    {
        if (connectionString != TestConnectionString) return;

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (connectionString != TestConnectionString) return;

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureDeletedAsync();
    }
}
