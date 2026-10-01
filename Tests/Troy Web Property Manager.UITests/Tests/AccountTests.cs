using OpenQA.Selenium;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.UITests.Infrastructure;
using Troy_Web_Property_Manager.UITests.Pages;
using Troy_Web_Property_Manager.UITests.Workflows;

namespace Troy_Web_Property_Manager.UITests.Tests
{
    /// <summary>
    /// Functional 1.a: sign up (choosing Applicant or Property Manager), confirm the email, log in, log out - and the
    /// ways each of those can fail. Technical 2.a: ASP.NET Identity users and roles decide what each user sees.
    /// </summary>
    [Collection(UiCollection.Name)]
    public sealed class AccountTests(UiFixture app)
    {
        [Theory]
        [InlineData(AppRoles.Applicant, new[] { "Home", "Privacy", "Available units", "My applications" })]
        [InlineData(AppRoles.PropertyManager, new[] { "Home", "Privacy", "Properties", "Applications", "Review queue" })]
        public void SignUp_WithARole_ConfirmTheEmail_ThenLogIn_ShowsThatRolesMenu(string role, string[] expectedMenu)
        {
            using var browser = app.NewBrowser();

            var user = browser.RegisterAndConfirm(app, role);
            browser.LogInAs(user);

            var nav = new NavBar(browser);
            Assert.Equal($"Hello {user.Email}!", nav.Greeting);
            Assert.Equal(expectedMenu, nav.Links);
        }

        // ---------------- Confirmation email: sent, not configured, or failed ----------------

        [Fact]
        public void SignUp_WithoutASendGridKey_ShowsTheConfirmLinkOnThePage_InsteadOfEmailingIt()
        {
            // The fixture's default: no SendGrid key, like a fresh clone.
            using var browser = app.NewBrowser();
            var user = new TestUser($"nokey-{Guid.NewGuid().ToString("N")[..8]}@uitest.local", UiFixture.Password, AppRoles.Applicant);

            new RegisterPage(browser).Open().Submit(user.Email, user.Password, role: user.Role);
            var confirmation = new RegisterConfirmationPage(browser).WaitUntilOpen();

            Assert.Equal($"We couldn't send a confirmation email to {user.Email}.", confirmation.Warning);
            Assert.True(confirmation.ShowsConfirmButton);
            Assert.Empty(app.Emails.To(user.Email));

            confirmation.Confirm();
            browser.LogInAs(user);
            Assert.Equal($"Hello {user.Email}!", new NavBar(browser).Greeting);
        }

        [Fact]
        public void SignUp_WhenTheEmailFailsToSend_ShowsTheConfirmLinkOnThePage()
        {
            using var sendGrid = app.SendGridConfigured(sendingFails: true);
            using var browser = app.NewBrowser();
            var user = new TestUser($"sendfail-{Guid.NewGuid().ToString("N")[..8]}@uitest.local", UiFixture.Password, AppRoles.PropertyManager);

            new RegisterPage(browser).Open().Submit(user.Email, user.Password, role: user.Role);
            var confirmation = new RegisterConfirmationPage(browser).WaitUntilOpen();

            Assert.Equal($"We couldn't send a confirmation email to {user.Email}.", confirmation.Warning);
            Assert.Empty(app.Emails.To(user.Email));

            confirmation.Confirm();
            browser.LogInAs(user);
            Assert.Equal($"Hello {user.Email}!", new NavBar(browser).Greeting);
        }

        [Fact]
        public void SignUp_WithASendGridKey_EmailsTheLink_AndDoesNotShowItOnThePage()
        {
            using var sendGrid = app.SendGridConfigured();
            using var browser = app.NewBrowser();
            var user = new TestUser($"emailed-{Guid.NewGuid().ToString("N")[..8]}@uitest.local", UiFixture.Password, AppRoles.Applicant);

            new RegisterPage(browser).Open().Submit(user.Email, user.Password, role: user.Role);
            var confirmation = new RegisterConfirmationPage(browser).WaitUntilOpen();

            Assert.Contains($"We sent a confirmation email to {user.Email}.", browser.PageText);
            Assert.Null(confirmation.Warning);
            Assert.False(confirmation.ShowsConfirmButton);

            browser.Driver.Navigate().GoToUrl(app.Emails.ConfirmationLink(user.Email));
            browser.WaitForText("Thank you for confirming your email.");
        }

        [Fact]
        public void TheConfirmLinkOnThePage_IsOnlyShownOnce_AndOnlyToTheBrowserThatRegistered()
        {
            // The link comes from TempData, not the email in the query string, so nobody can confirm someone
            // else's account just by knowing the address.
            using var browser = app.NewBrowser();
            var email = $"once-{Guid.NewGuid().ToString("N")[..8]}@uitest.local";
            new RegisterPage(browser).Open().Submit(email, UiFixture.Password, role: AppRoles.Applicant);
            var confirmation = new RegisterConfirmationPage(browser).WaitUntilOpen();
            Assert.True(confirmation.ShowsConfirmButton);
            var confirmationUrl = browser.PathAndQuery;

            browser.Reload();
            Assert.False(confirmation.ShowsConfirmButton);

            using var stranger = app.NewBrowser();
            stranger.Go(confirmationUrl);
            Assert.StartsWith(RegisterConfirmationPage.Path, stranger.PathAndQuery);
            Assert.False(new RegisterConfirmationPage(stranger).ShowsConfirmButton);
        }

        [Fact]
        public void LogIn_BeforeConfirmingTheEmail_IsRefused()
        {
            using var browser = app.NewBrowser();
            var email = $"unconfirmed-{Guid.NewGuid().ToString("N")[..8]}@uitest.local";
            new RegisterPage(browser).Open().Submit(email, UiFixture.Password, role: AppRoles.Applicant);
            new RegisterConfirmationPage(browser).WaitUntilOpen();

            var login = new LoginPage(browser).Open();
            login.Submit(email, UiFixture.Password);

            Assert.Contains("You must confirm your email before you can log in.", login.Errors);
            Assert.False(new NavBar(browser).IsSignedIn);
        }

        [Theory]
        [InlineData("wrong-password")]
        [InlineData("unknown-user")]
        public void LogIn_WithBadCredentials_ShowsInvalidLoginAttempt(string mistake)
        {
            using var browser = app.NewBrowser();
            var user = UiFixture.SeededApplicant;

            var login = new LoginPage(browser).Open();
            login.Submit(mistake == "unknown-user" ? "nobody@uitest.local" : user.Email, mistake == "wrong-password" ? "Wrong#Password1" : user.Password);

            Assert.Contains("Invalid login attempt.", login.Errors);
            Assert.False(new NavBar(browser).IsSignedIn);
        }

        [Fact]
        public void LogIn_WithEmptyFields_ShowsRequiredErrors()
        {
            using var browser = app.NewBrowser();
            new LoginPage(browser).Open();

            browser.Click(By.XPath(Xp.Button("Log in")));

            browser.WaitForText("The Email field is required.");
            browser.WaitForText("The Password field is required.");
            Assert.StartsWith(LoginPage.Path, browser.PathAndQuery);
        }

        [Theory]
        [InlineData("not-an-email", "Valid#Pass1", "Valid#Pass1", AppRoles.Applicant, "The Email field is not a valid e-mail address.")]
        [InlineData("ok@uitest.local", "Valid#Pass1", "Different#Pass1", AppRoles.Applicant, "The password and confirmation password do not match.")]
        [InlineData("ok@uitest.local", "abc", "abc", AppRoles.Applicant, "The Password must be at least 6 and at most 100 characters long.")]
        [InlineData("ok@uitest.local", "Valid#Pass1", "Valid#Pass1", null, "Please select a role.")]
        public void SignUp_WithInvalidInput_ShowsTheErrorAndStaysOnTheForm(string email, string password, string confirm, string? role, string expected)
        {
            using var browser = app.NewBrowser();
            var register = new RegisterPage(browser).Open();

            register.Submit(email, password, confirm, role);

            browser.WaitForText(expected);
            Assert.StartsWith(RegisterPage.Path, browser.PathAndQuery);
        }

        [Fact]
        public void SignUp_WithAWeakPassword_IsRejectedByTheIdentityPasswordRules()
        {
            using var browser = app.NewBrowser();
            var register = new RegisterPage(browser).Open();

            register.Submit($"weak-{Guid.NewGuid().ToString("N")[..8]}@uitest.local", "password", role: AppRoles.Applicant);

            browser.WaitForText("Passwords must have at least one non alphanumeric character.");
            Assert.Contains("Passwords must have at least one digit ('0'-'9').", browser.PageText);
            Assert.Contains("Passwords must have at least one uppercase ('A'-'Z').", browser.PageText);
        }

        [Fact]
        public void SignUp_WithAnEmailThatIsAlreadyRegistered_IsRejected()
        {
            using var browser = app.NewBrowser();
            var register = new RegisterPage(browser).Open();

            register.Submit(UiFixture.SeededApplicant.Email, UiFixture.Password, role: AppRoles.Applicant);

            browser.WaitForText($"Username '{UiFixture.SeededApplicant.Email}' is already taken.");
        }

        [Fact]
        public void SignUp_WithATamperedRole_IsRejectedByTheServer()
        {
            using var browser = app.NewBrowser();
            var register = new RegisterPage(browser).Open();
            // Change the Applicant radio's value to a role that doesn't exist, the way a hand-made post would.
            browser.Js("document.getElementById('role-Applicant').value = 'Administrator';");

            register.SubmitSkippingBrowserValidation($"tamper-{Guid.NewGuid().ToString("N")[..8]}@uitest.local", UiFixture.Password, UiFixture.Password, AppRoles.Applicant);

            Assert.Contains("Please select a valid role.", register.Errors);
            Assert.StartsWith(RegisterPage.Path, browser.PathAndQuery);
        }

        [Fact]
        public async Task LogOut_EndsTheSession()
        {
            using var browser = app.NewBrowser();
            browser.LogInAs(await app.CreateApplicantAsync());

            browser.LogOut();

            Assert.False(new NavBar(browser).IsSignedIn);
            browser.Go(ApplicationListPage.Path);
            Assert.StartsWith(LoginPage.Path, browser.PathAndQuery);
        }

        [Fact]
        public void SeededDemoAccounts_CanLogIn_InBothRoles()
        {
            // Technical 2.b.ii: the seeded property managers and applicants work.
            using var manager = app.NewBrowserAs(UiFixture.SeededManager);
            using var applicant = app.NewBrowserAs(UiFixture.SeededApplicant);

            Assert.Contains("Review queue", new NavBar(manager).Links);
            Assert.Contains("Available units", new NavBar(applicant).Links);
        }
    }
}
