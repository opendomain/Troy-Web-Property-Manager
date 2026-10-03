using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Troy_Web_Property_Manager.Areas.Identity.Pages.Account
{
    /// <summary>
    /// "Check your email" page shown after sign-up. Replaces the default Identity UI page so that, when the
    /// confirmation email couldn't be sent, the user gets the confirmation link right here instead.
    /// </summary>
    /// <remarks>
    /// The link only ever comes from TempData, set by Register in the same browser. We deliberately don't generate a
    /// token from the email in the query string (the default page does that for the no-op sender), since that would
    /// let anyone confirm any unconfirmed account just by knowing its email address.
    /// </remarks>
    [AllowAnonymous]
    public class RegisterConfirmationModel : PageModel
    {
        public const string ConfirmationLinkKey = "ConfirmationLink";
        public const string EmailFailedKey = "ConfirmationEmailFailed";

        public string? Email { get; set; }

        /// <summary>
        /// Set when the confirmation email failed to send, or when Registration:ShowConfirmationLink is on.
        /// </summary>
        public string? ConfirmationLink { get; set; }

        /// <summary>True when the confirmation email couldn't be sent.</summary>
        public bool EmailFailed { get; set; }

        public IActionResult OnGet(string? email = null)
        {
            if (email is null)
            {
                return RedirectToPage("/Index", new { area = "" });
            }

            Email = email;
            ConfirmationLink = TempData[ConfirmationLinkKey] as string;
            EmailFailed = TempData[EmailFailedKey] is true;
            return Page();
        }
    }
}
