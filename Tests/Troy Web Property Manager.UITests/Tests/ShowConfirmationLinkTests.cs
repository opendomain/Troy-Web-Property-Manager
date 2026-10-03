using OpenQA.Selenium;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.UITests.Infrastructure;
using Troy_Web_Property_Manager.UITests.Pages;
using Troy_Web_Property_Manager.UITests.Workflows;

namespace Troy_Web_Property_Manager.UITests.Tests
{
    /// <summary>
    /// Sign-up with Registration:ShowConfirmationLink on. SendGrid can accept an email that never arrives, so the
    /// confirmation page offers the link even when the email went out.
    /// </summary>
    [Collection(ShowConfirmationLinkUiCollection.Name)]
    public sealed class ShowConfirmationLinkTests(ShowConfirmationLinkUiFixture app)
    {
        [Fact]
        public void SignUp_WhenTheEmailIsSent_StillShowsTheConfirmationLinkOnThePage()
        {
            using var browser = app.NewBrowser();
            var user = new TestUser($"showlink-{Guid.NewGuid().ToString("N")[..10]}@uitest.local", UiFixture.Password, AppRoles.Applicant);

            new RegisterPage(browser).Open().Submit(user.Email, user.Password, role: user.Role);
            browser.WaitUntil(() => browser.PathAndQuery.StartsWith("/Identity/Account/RegisterConfirmation"), "the registration confirmation page");

            // The email went out, and the page says so - but the link is there too.
            browser.WaitForText($"We sent a confirmation email to {user.Email}.");
            Assert.DoesNotContain("We couldn't send a confirmation email", browser.PageText);
            Assert.Single(app.Emails.To(user.Email));
            var link = browser.Driver.FindElement(By.LinkText("Confirm your account")).GetAttribute("href")!;

            browser.Driver.Navigate().GoToUrl(link);
            browser.WaitForText("Thank you for confirming your email.");
            browser.LogInAs(user);
            Assert.Contains("My applications", new NavBar(browser).Links);
        }
    }
}
