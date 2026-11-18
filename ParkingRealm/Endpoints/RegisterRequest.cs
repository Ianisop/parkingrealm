namespace ParkingRealm.Endpoints;

public record RegisterRequest(
    string? Email,
    string? Password,
    string? FirstName,
    string? LastName,
    string? PhoneNumber)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        UserValidation.ValidateEmail(errors, Email);

        if (string.IsNullOrEmpty(Password))
            errors["password"] = ["Password is required."];

        UserValidation.ValidateName(errors, "firstName", "First name", FirstName);
        UserValidation.ValidateName(errors, "lastName", "Last name", LastName);
        UserValidation.ValidatePhoneNumber(errors, PhoneNumber);

        return errors;
    }
}
