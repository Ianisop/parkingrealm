using Microsoft.AspNetCore.Identity;

namespace ParkingRealm.Data;

public class ApplicationUser : IdentityUser
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }

    // Set when the account was deleted by an administrator and its personal data anonymized.
    public DateTimeOffset? DeletedAt { get; set; }
}
