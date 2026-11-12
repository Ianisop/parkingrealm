namespace ParkingRealm.RegistryMock;

public record RegistryVehicle(string RegistrationNumber, string Make, string Model);

public static class RegistryMockApp
{
    public const string DefaultUrl = "http://localhost:5199";

    public static readonly IReadOnlyDictionary<string, RegistryVehicle> Vehicles =
        new Dictionary<string, RegistryVehicle>(StringComparer.OrdinalIgnoreCase)
        {
            ["AB12345"] = new("AB12345", "Volkswagen", "Golf"),
            ["CD12345"] = new("CD12345", "Toyota", "Yaris"),
            ["EF12345"] = new("EF12345", "Tesla", "Model 3"),
        };

    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        if (string.IsNullOrEmpty(builder.Configuration["urls"]))
            builder.WebHost.UseUrls(DefaultUrl);

        var app = builder.Build();
        app.MapGet("/vehicles/{registrationNumber}", (string registrationNumber) =>
            Vehicles.TryGetValue(registrationNumber.Trim(), out var vehicle)
                ? Results.Ok(vehicle)
                : Results.NotFound());
        return app;
    }
}
