namespace ParkingRealm.Endpoints;

public record LoginRequest(string? Email, string? Password)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(Email))
            errors["email"] = ["Email is required."];

        if (string.IsNullOrEmpty(Password))
            errors["password"] = ["Password is required."];

        return errors;
    }
}
