using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ParkingRealm.Data;

namespace ParkingRealm.Endpoints;

// Rules shared by self-registration and administrator user management.
public static class UserValidation
{
    // E.164: '+' followed by country code and number, at most 15 digits.
    private static readonly Regex PhoneRegex = new(@"^\+[1-9]\d{1,14}$", RegexOptions.Compiled);

    public static void ValidateEmail(Dictionary<string, string[]> errors, string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            errors["email"] = ["Email is required."];
        else if (!new EmailAddressAttribute().IsValid(email))
            errors["email"] = ["Email is not a valid email address."];
    }

    public static void ValidateName(Dictionary<string, string[]> errors, string key, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors[key] = [$"{label} is required."];
        else if (value.Trim().Length > ApplicationDbContext.NameMaxLength)
            errors[key] = [$"{label} must be at most {ApplicationDbContext.NameMaxLength} characters."];
    }

    public static void ValidatePhoneNumber(Dictionary<string, string[]> errors, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            errors["phoneNumber"] = ["Phone number is required."];
        else if (!PhoneRegex.IsMatch(phoneNumber))
            errors["phoneNumber"] = ["Phone number must include a country code, for example +4512345678."];
    }

    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    public static IResult DuplicateEmail() =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Email is already in use."] });

    public static Dictionary<string, string[]> MapIdentityErrors(IEnumerable<IdentityError> identityErrors)
    {
        var errors = identityErrors.ToList();
        var result = new Dictionary<string, string[]>();

        if (errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
            result["email"] = ["Email is already in use."];

        var passwordErrors = errors.Where(e => e.Code.StartsWith("Password")).Select(e => e.Description).ToArray();
        if (passwordErrors.Length > 0)
            result["password"] = passwordErrors;

        var other = errors
            .Where(e => e.Code is not ("DuplicateEmail" or "DuplicateUserName") && !e.Code.StartsWith("Password"))
            .Select(e => e.Description)
            .ToArray();
        if (other.Length > 0)
            result["registration"] = other;

        return result;
    }
}
