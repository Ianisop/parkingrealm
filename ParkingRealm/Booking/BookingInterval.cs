using System.Globalization;
using System.Text.RegularExpressions;

namespace ParkingRealm.Booking;

public readonly record struct BookingInterval(DateTimeOffset Start, DateTimeOffset End)
{
    public static Dictionary<string, string[]> Validate(
        string? start, string? end, DateTimeOffset now, bool requireFuture, out BookingInterval interval)
    {
        var errors = new Dictionary<string, string[]>();
        var validStart = TryParseInstant(start, out var from);
        var validEnd = TryParseInstant(end, out var until);
        if (!validStart)
            errors["start"] = ["Start must be an ISO timestamp with an explicit time zone."];
        if (!validEnd)
            errors["end"] = ["End must be an ISO timestamp with an explicit time zone."];
        if (validStart && validEnd && until <= from)
            errors["end"] = ["End must be later than start."];
        if (validStart && requireFuture && from <= now)
            errors["start"] = ["Start must be in the future."];

        interval = new BookingInterval(from, until);
        return errors;
    }

    private static bool TryParseInstant(string? text, out DateTimeOffset instant)
    {
        instant = default;
        if (string.IsNullOrWhiteSpace(text) || !text.Contains('T') ||
            !Regex.IsMatch(text, @"(Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase) ||
            !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return false;
        instant = parsed.ToUniversalTime();
        return true;
    }
}
