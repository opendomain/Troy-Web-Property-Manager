using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;

namespace Troy_Web_Property_Manager.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Sign-up page scaffolded from the Identity UI, with a role picker added.
    /// We don't trust the posted role - it has to be in <see cref="AppRoles.All"/>.
    /// </summary>
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly SendGridOptions _sendGridOptions;

        public RegisterModel(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            IOptions<SendGridOptions> sendGridOptions)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _sendGridOptions = sendGridOptions.Value;
        }

        [BindProperty]
        public InputModel Input { get; set; } = default!;

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; } = "";

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at most {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; } = "";

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; } = "";

            /// <summary>"Applicant" or "Property Manager" - shown as radio buttons (1.a.i).</summary>
            [Required(ErrorMessage = "Please select a role.")]
            [Display(Name = "I am a")]
            public string Role { get; set; } = "";
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ReturnUrl = returnUrl;

            // Only accept one of our two roles, no matter what the form sent.
            if (!AppRoles.All.Contains(Input.Role))
            {
                ModelState.AddModelError(nameof(Input) + "." + nameof(Input.Role), "Please select a valid role.");
            }

            if (ModelState.IsValid)
            {
                var user = new IdentityUser { UserName = Input.Email, Email = Input.Email };
                var result = await _userManager.CreateAsync(user, Input.Password);
                if (result.Succeeded)
                {
                    // Give them the role straight away - the controllers' [Authorize(Roles = ...)] depends on it.
                    result = await _userManager.AddToRoleAsync(user, Input.Role);
                    if (!result.Succeeded)
                    {
                        // Don't leave an account behind without a role.
                        await _userManager.DeleteAsync(user);
                    }
                }
                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password and role {Role}.", Input.Role);

                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = user.Id, code, returnUrl },
                        protocol: Request.Scheme)!;

                    // If the email can't go out, keep the account and let RegisterConfirmation show the link on the page
                    // instead. TempData is encrypted, one-time and tied to this browser, so only the person who just
                    // registered sees it. No API key counts as a failure too - in Development EmailSender only logs the
                    // email then, so nothing would actually be sent.
                    if (string.IsNullOrWhiteSpace(_sendGridOptions.ApiKey))
                    {
                        _logger.LogWarning("SendGrid isn't configured; showing the confirmation link on the page instead.");
                        TempData[RegisterConfirmationModel.ConfirmationLinkKey] = callbackUrl;
                    }
                    else
                    {
                        try
                        {
                            await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send confirmation email; showing the confirmation link on the page instead.");
                            TempData[RegisterConfirmationModel.ConfirmationLinkKey] = callbackUrl;
                        }
                    }

                    // If account confirmation is required, redirect to register confirmation page
                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("/Account/RegisterConfirmation", new { area = "Identity", email = Input.Email, returnUrl });
                    }

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
