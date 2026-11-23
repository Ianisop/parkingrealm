using System.Text.Json;

namespace ParkingRealm.Registry;

public enum RegistryOutcome
{
    Found,
    NotFound,
    Unavailable
}

public interface IVehicleRegistry
{
    Task<RegistryOutcome> LookupAsync(string registrationNumber, CancellationToken ct);
}

public class HttpVehicleRegistry(HttpClient http, ILogger<HttpVehicleRegistry> logger) : IVehicleRegistry
{
    public async Task<RegistryOutcome> LookupAsync(string registrationNumber, CancellationToken ct)
    {
        if (http.BaseAddress is null)
        {
            logger.LogError("VehicleRegistry:BaseUrl is not configured.");
            return RegistryOutcome.Unavailable;
        }

        try
        {
            using var response = await http.GetAsync($"vehicles/{Uri.EscapeDataString(registrationNumber)}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return RegistryOutcome.NotFound;
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Vehicle registry returned {StatusCode}.", (int)response.StatusCode);
                return RegistryOutcome.Unavailable;
            }

            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var returned = body.RootElement.ValueKind == JsonValueKind.Object &&
                body.RootElement.TryGetProperty("registrationNumber", out var number) &&
                number.ValueKind == JsonValueKind.String ? number.GetString()?.Trim() : null;
            return string.Equals(returned, registrationNumber, StringComparison.OrdinalIgnoreCase)
                ? RegistryOutcome.Found
                : RegistryOutcome.NotFound;
        }
        catch (JsonException)
        {
            return RegistryOutcome.NotFound;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Vehicle registry is unreachable or timed out.");
            return RegistryOutcome.Unavailable;
        }
    }
}
