using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using ParkingRealm.Data;

namespace ParkingRealm.Endpoints;

public record RegisterRequest(
    string? Email,
    string? Password,
    string? FirstName,
    string? LastName,
    string? PhoneNumber)
{
    // E.164: '+' followed by country code and number, at most 15 digits.
    private static readonly Regex PhoneRegex = new(@"^\+[1-9]\d{1,14}$", RegexOptions.Compiled);

    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(Email))
            errors["email"] = ["Email is required."];
        else if (!new EmailAddressAttribute().IsValid(Email))
            errors["email"] = ["Email is not a valid email address."];

        if (string.IsNullOrEmpty(Password))
            errors["password"] = ["Password is required."];

        ValidateName(errors, "firstName", "First name", FirstName);
        ValidateName(errors, "lastName", "Last name", LastName);

        if (string.IsNullOrWhiteSpace(PhoneNumber))
            errors["phoneNumber"] = ["Phone number is required."];
        else if (!PhoneRegex.IsMatch(PhoneNumber))
            errors["phoneNumber"] = ["Phone number must include a country code, for example +4512345678."];

        return errors;
    }

    private static void ValidateName(Dictionary<string, string[]> errors, string key, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors[key] = [$"{label} is required."];
        else if (value.Trim().Length > ApplicationDbContext.NameMaxLength)
            errors[key] = [$"{label} must be at most {ApplicationDbContext.NameMaxLength} characters."];
    }
}
