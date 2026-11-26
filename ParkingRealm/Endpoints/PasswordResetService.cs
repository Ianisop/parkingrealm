using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using ParkingRealm.Data;

namespace ParkingRealm.Endpoints;

// Delivers a reset link to the user. Replace the implementation to send real e-mail.
public interface IPasswordResetSender
{
    Task SendAsync(ApplicationUser user, string resetLink, CancellationToken ct);
}

// Simulates e-mail: the link is only written to the server log. The link is a credential, so a production sender must not log it.
public class LoggingPasswordResetSender(ILogger<LoggingPasswordResetSender> logger) : IPasswordResetSender
{
    public Task SendAsync(ApplicationUser user, string resetLink, CancellationToken ct)
    {
        logger.LogInformation("Simulated password reset e-mail to {Email}: {ResetLink}", user.Email, resetLink);
        return Task.CompletedTask;
    }
}

public class PasswordResetService(
    UserManager<ApplicationUser> userManager, IPasswordResetSender sender, IConfiguration configuration)
{
    public async Task IssueAsync(ApplicationUser user, CancellationToken ct)
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var baseUrl = (configuration["PasswordReset:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
        var link = $"{baseUrl}/?resetToken={WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))}" +
                   $"&email={Uri.EscapeDataString(user.Email!)}";
        await sender.SendAsync(user, link, ct);
    }

    public static bool TryDecodeToken(string encoded, out string token)
    {
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded));
            return true;
        }
        catch (FormatException)
        {
            token = "";
            return false;
        }
    }
}
