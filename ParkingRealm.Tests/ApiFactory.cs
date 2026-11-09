using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingRealm.Data;

namespace ParkingRealm.Tests;

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
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);

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
