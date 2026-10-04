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
    /// Validates the posted role against the roles allowed for this environment.
    /// </summary>
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly SendGridOptions _sendGridOptions;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public RegisterModel(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            IOptions<SendGridOptions> sendGridOptions,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _sendGridOptions = sendGridOptions.Value;
            _configuration = configuration;
            _environment = environment;
        }

        public string[] RegistrationRoles => _environment.IsDevelopment() ? AppRoles.All : [AppRoles.Applicant];

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
            // Outside Development Applicant is the only choice, so pick it rather than make people click it.
            if (RegistrationRoles.Length == 1)
            {
                Input = new InputModel { Role = RegistrationRoles[0] };
            }
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ReturnUrl = returnUrl;

            // Public manager registration is a Development-only convenience, including for forged posts.
            if (!RegistrationRoles.Contains(Input.Role))
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

                    // Delivery failures keep the account unconfirmed. Only Development may expose a direct link.
                    var emailSent = false;
                    if (string.IsNullOrWhiteSpace(_sendGridOptions.ApiKey))
                    {
                        _logger.LogWarning("SendGrid isn't configured; the confirmation email was not sent.");
                    }
                    else
                    {
                        try
                        {
                            await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
                            emailSent = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send confirmation email; the account remains unconfirmed.");
                        }
                    }

                    // The environment gate applies even if ShowConfirmationLink is accidentally enabled in Production.
                    if (!emailSent)
                    {
                        TempData[RegisterConfirmationModel.EmailFailedKey] = true;
                    }
                    if (_environment.IsDevelopment() &&
                        (!emailSent || _configuration.GetValue<bool>("Registration:ShowConfirmationLink")))
                    {
                        TempData[RegisterConfirmationModel.ConfirmationLinkKey] = callbackUrl;
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
