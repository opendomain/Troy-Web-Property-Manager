using Microsoft.AspNetCore.Identity;
using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.Data
{
    /// <summary>
    /// Makes the first Property Manager outside Development, where public sign-up only creates applicants: register and
    /// confirm an account as usual, set Bootstrap:ManagerEmail to its address, and restart. Program runs this on startup.
    /// </summary>
    /// <remarks>
    /// The account becomes a Property Manager and stops being an Applicant. Unconfirmed accounts are left alone, so
    /// nobody can claim the role by registering the address before its owner does. It does nothing once the account is
    /// a manager, so the setting can stay until you remove it.
    /// </remarks>
    public static class ManagerBootstrapper
    {
        /// <returns>True if <paramref name="email"/> was promoted just now.</returns>
        public static async Task<bool> PromoteAsync(UserManager<IdentityUser> userManager, string? email, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;

            var user = await userManager.FindByEmailAsync(email);
            if (user is null || !user.EmailConfirmed)
            {
                logger.LogWarning("Bootstrap:ManagerEmail is set, but {Email} isn't a confirmed account yet. Register and confirm it, then restart.", email);
                return false;
            }
            if (await userManager.IsInRoleAsync(user, AppRoles.PropertyManager)) return false;

            var result = await userManager.AddToRoleAsync(user, AppRoles.PropertyManager);
            if (result.Succeeded && await userManager.IsInRoleAsync(user, AppRoles.Applicant))
            {
                result = await userManager.RemoveFromRoleAsync(user, AppRoles.Applicant);
            }
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Couldn't make {email} a Property Manager: " +
                    string.Join(" ", result.Errors.Select(e => e.Description)));
            }
            logger.LogInformation("Made {Email} a Property Manager (Bootstrap:ManagerEmail).", email);
            return true;
        }
    }
}
