using Microsoft.AspNetCore.Identity;

namespace ParkingRealm.Data;

public static class AdministratorProvisioning
{
    public const string RoleName = "Administrator";

    public static async Task<IdentityResult> GrantAsync(IServiceProvider services, string email)
    {
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null)
            return IdentityResult.Failed(new IdentityError { Code = "UserNotFound", Description = "Register the account before provisioning it." });
        if (!await roles.RoleExistsAsync(RoleName))
        {
            var created = await roles.CreateAsync(new IdentityRole(RoleName));
            if (!created.Succeeded) return created;
        }
        return await users.IsInRoleAsync(user, RoleName) ? IdentityResult.Success : await users.AddToRoleAsync(user, RoleName);
    }
}
