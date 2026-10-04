using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Troy_Web_Property_Manager.Areas.Identity.Pages.Account;
using Troy_Web_Property_Manager.Areas.Identity.Pages.Account.Manage;
using Troy_Web_Property_Manager.Models;
using ErrorModel = Troy_Web_Property_Manager.Pages.ErrorModel;
using Troy_Web_Property_Manager.Services;

namespace Troy_Web_Property_Manager.Tests.Controllers
{
    /// <summary>
    /// The Identity pages' code-behind, against real Identity (<see cref="IdentityTestHost"/>): the outcomes the UI
    /// tests don't reach, such as lockout, two-factor, Identity refusing a change, and the Manage pages.
    /// </summary>
    public sealed class IdentityPageTests : IAsyncDisposable
    {
        private const string Email = "someone@example.com";

        private IdentityTestHost _host = new();

        public async ValueTask DisposeAsync()
        {
            await _host.DisposeAsync();
        }

        private async Task UseHostAsync(IdentityTestHost host)
        {
            await _host.DisposeAsync();
            _host = host;
        }

        // ---------------- Login ----------------

        private LoginModel Login(string password = IdentityTestHost.Password)
        {
            var page = _host.Page(new LoginModel(_host.SignIn, NullLogger<LoginModel>.Instance));
            page.Input = new LoginModel.InputModel { Email = Email, Password = password };
            return page;
        }

        [Fact]
        public async Task Login_Succeeds_AndGoesHomeWithoutAReturnUrl()
        {
            await _host.CreateUserAsync(Email);

            var result = await Login().OnPostAsync(returnUrl: null);

            Assert.Equal("~/", Assert.IsType<LocalRedirectResult>(result).Url);
            Assert.Contains(".AspNetCore.Identity.Application=", _host.Cookies);
        }

        [Fact]
        public async Task Login_WithTwoFactorOn_GoesToTheSecondStep()
        {
            var user = await _host.CreateUserAsync(Email);
            await _host.Users.SetTwoFactorEnabledAsync(user, true);

            var result = await Login().OnPostAsync("/Applications");

            var redirect = Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("/Account/LoginWith2fa", redirect.PageName);
            Assert.Equal("/Applications", redirect.RouteValues!["returnUrl"]);
        }

        [Fact]
        public async Task Login_LockedOutAccount_GoesToTheLockoutPage()
        {
            var user = await _host.CreateUserAsync(Email);
            await _host.Users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddHours(1));

            var result = await Login().OnPostAsync("/");

            Assert.Equal("/Account/Lockout", Assert.IsType<RedirectToPageResult>(result).PageName);
        }

        [Fact]
        public async Task Login_TooManyWrongPasswords_LocksTheAccount()
        {
            await _host.CreateUserAsync(Email);

            IActionResult result = new EmptyResult();
            for (var attempt = 0; attempt < 5; attempt++) result = await Login("Wrong#2026").OnPostAsync("/");

            Assert.Equal("/Account/Lockout", Assert.IsType<RedirectToPageResult>(result).PageName);
        }

        [Fact]
        public async Task Login_WrongPassword_RedisplaysWithAnError()
        {
            await _host.CreateUserAsync(Email);
            var page = Login("Wrong#2026");

            Assert.IsType<PageResult>(await page.OnPostAsync("/"));
            Assert.Equal("Invalid login attempt.", Assert.Single(page.ModelState[""]!.Errors).ErrorMessage);
        }

        // ---------------- Logout ----------------

        private LogoutModel Logout()
        {
            return _host.Page(new LogoutModel(_host.SignIn, NullLogger<LogoutModel>.Instance));
        }

        [Fact]
        public async Task Logout_SignsOut_AndGoesToTheReturnUrl_OrReloads()
        {
            Logout().OnGet();

            Assert.Equal("/", Assert.IsType<LocalRedirectResult>(await Logout().OnPost("/")).Url);
            Assert.IsType<RedirectToPageResult>(await Logout().OnPost(returnUrl: null));
            Assert.Contains(".AspNetCore.Identity.Application=;", _host.Cookies);
        }

        // ---------------- Register ----------------

        private sealed class NoEmail : IEmailSender
        {
            public Task SendEmailAsync(string email, string subject, string htmlMessage)
            {
                return Task.CompletedTask;
            }
        }

        private RegisterModel Register()
        {
            var page = _host.Page(new RegisterModel(_host.Users, _host.SignIn, NullLogger<RegisterModel>.Instance, new NoEmail(),
                Options.Create(new SendGridOptions { ApiKey = "SG.test" }), new ConfigurationBuilder().Build(),
                new TestWebHostEnvironment("Production")));
            page.Input = new RegisterModel.InputModel
            {
                Email = Email,
                Password = IdentityTestHost.Password,
                ConfirmPassword = IdentityTestHost.Password,
                Role = AppRoles.Applicant
            };
            return page;
        }

        [Fact]
        public async Task Register_SendsThemToCheckTheirEmail()
        {
            var result = await Register().OnPostAsync(returnUrl: null);

            var redirect = Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("/Account/RegisterConfirmation", redirect.PageName);
            Assert.Equal("~/", redirect.RouteValues!["returnUrl"]);
        }

        [Fact]
        public async Task Register_WhenTheRoleCantBeGiven_RemovesTheAccountAgain()
        {
            await UseHostAsync(new IdentityTestHost(rejectUpdates: true));
            var page = Register();

            Assert.IsType<PageResult>(await page.OnPostAsync("/"));

            Assert.Equal("Refused by the test.", Assert.Single(page.ModelState[""]!.Errors).ErrorMessage);
            Assert.Null(await _host.Users.FindByEmailAsync(Email));
        }

        [Fact]
        public async Task Register_WithoutRequiredConfirmation_SignsStraightIn()
        {
            // The app always requires a confirmed account; this is the scaffolded page's other path.
            await UseHostAsync(new IdentityTestHost(requireConfirmedAccount: false));

            var result = await Register().OnPostAsync("/Units");

            Assert.Equal("/Units", Assert.IsType<LocalRedirectResult>(result).Url);
            Assert.Contains(".AspNetCore.Identity.Application=", _host.Cookies);
        }

        [Fact]
        public void RegisterConfirmation_WithoutAnEmail_GoesHome()
        {
            var page = _host.Page(new RegisterConfirmationModel(new TestWebHostEnvironment("Production")));

            Assert.Equal("/Index", Assert.IsType<RedirectToPageResult>(page.OnGet(email: null)).PageName);
        }

        // ---------------- Manage: profile ----------------

        private IndexModel Profile(IdentityUser? user)
        {
            return _host.Page(new IndexModel(_host.Users, _host.SignIn), user);
        }

        [Fact]
        public async Task Profile_ShowsTheUsernameAndPhone()
        {
            var user = await _host.CreateUserAsync(Email);
            await _host.Users.SetPhoneNumberAsync(user, "518-555-0100");
            var page = Profile(user);

            Assert.IsType<PageResult>(await page.OnGetAsync());

            Assert.Equal(Email, page.Username);
            Assert.Equal("518-555-0100", page.Input.PhoneNumber);
        }

        [Fact]
        public async Task Profile_SavesANewPhoneNumber()
        {
            var user = await _host.CreateUserAsync(Email);
            var page = Profile(user);
            page.Input = new IndexModel.InputModel { PhoneNumber = "518-555-0111" };

            Assert.IsType<RedirectToPageResult>(await page.OnPostAsync());

            Assert.Equal("Your profile has been updated.", page.StatusMessage);
            Assert.Equal("518-555-0111", await _host.Users.GetPhoneNumberAsync((await _host.Users.FindByEmailAsync(Email))!));
        }

        [Fact]
        public async Task Profile_SavedUnchanged_JustConfirms()
        {
            var user = await _host.CreateUserAsync(Email);
            var page = Profile(user);
            page.Input = new IndexModel.InputModel { PhoneNumber = null };

            Assert.IsType<RedirectToPageResult>(await page.OnPostAsync());
            Assert.Equal("Your profile has been updated.", page.StatusMessage);
        }

        [Fact]
        public async Task Profile_InvalidPhone_RedisplaysTheForm()
        {
            var user = await _host.CreateUserAsync(Email);
            var page = Profile(user);
            page.ModelState.AddModelError("Input.PhoneNumber", "The Phone number field is not a valid phone number.");

            Assert.IsType<PageResult>(await page.OnPostAsync());
            Assert.Equal(Email, page.Username);
        }

        [Fact]
        public async Task Profile_IdentityRefusesThePhone_SaysSo()
        {
            await UseHostAsync(new IdentityTestHost(rejectUpdates: true));
            var user = await _host.CreateUserAsync(Email);
            var page = Profile(user);
            page.Input = new IndexModel.InputModel { PhoneNumber = "518-555-0111" };

            Assert.IsType<RedirectToPageResult>(await page.OnPostAsync());
            Assert.Equal("Unexpected error when trying to set phone number.", page.StatusMessage);
        }

        [Fact]
        public async Task Profile_AccountThatNoLongerExists_IsNotFound()
        {
            var gone = new IdentityUser { Id = "deleted-user" };

            Assert.IsType<NotFoundObjectResult>(await Profile(gone).OnGetAsync());
            Assert.IsType<NotFoundObjectResult>(await Profile(gone).OnPostAsync());
        }

        // ---------------- Manage: change password ----------------

        private ChangePasswordModel ChangePassword(IdentityUser? user, string oldPassword = IdentityTestHost.Password)
        {
            var page = _host.Page(new ChangePasswordModel(_host.Users, _host.SignIn), user);
            page.Input = new ChangePasswordModel.InputModel { OldPassword = oldPassword, NewPassword = "Changed#2026", ConfirmPassword = "Changed#2026" };
            return page;
        }

        [Fact]
        public async Task ChangePassword_ChangesIt_AndSaysSo()
        {
            var user = await _host.CreateUserAsync(Email);
            var page = ChangePassword(user);
            page.OnGet();

            Assert.IsType<RedirectToPageResult>(await page.OnPostAsync());

            Assert.Equal("Your password has been changed.", page.StatusMessage);
            Assert.True(await _host.Users.CheckPasswordAsync((await _host.Users.FindByEmailAsync(Email))!, "Changed#2026"));
        }

        [Fact]
        public async Task ChangePassword_WrongCurrentPassword_RedisplaysWithTheError()
        {
            var user = await _host.CreateUserAsync(Email);
            var page = ChangePassword(user, oldPassword: "Wrong#2026");

            Assert.IsType<PageResult>(await page.OnPostAsync());
            Assert.Equal("Incorrect password.", Assert.Single(page.ModelState[""]!.Errors).ErrorMessage);
        }

        [Fact]
        public async Task ChangePassword_InvalidForm_RedisplaysIt()
        {
            var user = await _host.CreateUserAsync(Email);
            var page = ChangePassword(user);
            page.ModelState.AddModelError("Input.ConfirmPassword", "The new password and confirmation password do not match.");

            Assert.IsType<PageResult>(await page.OnPostAsync());
        }

        [Fact]
        public async Task ChangePassword_AccountThatNoLongerExists_IsNotFound()
        {
            Assert.IsType<NotFoundObjectResult>(await ChangePassword(new IdentityUser { Id = "deleted-user" }).OnPostAsync());
        }

        // ---------------- Error page ----------------

        [Fact]
        public void ErrorPage_ShowsTheTraceId_OrTheRequestId()
        {
            var http = new DefaultHttpContext { TraceIdentifier = "request-1" };
            var page = new ErrorModel { PageContext = new PageContext { HttpContext = http } };

            using (var activity = new Activity("test").Start())
            {
                page.OnGet();
                Assert.Equal(activity.Id, page.RequestId);
            }
            Assert.Null(Activity.Current);
            page.OnGet();

            Assert.Equal("request-1", page.RequestId);
            Assert.True(page.ShowRequestId);
        }
    }
}
