using OpenQA.Selenium;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.UITests.Infrastructure;
using Troy_Web_Property_Manager.UITests.Pages;
using Troy_Web_Property_Manager.UITests.Workflows;

namespace Troy_Web_Property_Manager.UITests.Tests
{
    /// <summary>
    /// Sign-up on an app with no SendGrid key. Register doesn't even try to send then (in Development EmailSender
    /// would only log it), so it has to fall back to showing the confirmation link, the same as when a send fails.
    /// </summary>
    [Collection(NoSendGridKeyUiCollection.Name)]
    public sealed class NoSendGridKeyTests(NoSendGridKeyUiFixture app)
    {
        [Fact]
        public void SignUp_WithNoSendGridKey_ShowsTheConfirmationLinkOnThePage()
        {
            using var browser = app.NewBrowser();
            var user = new TestUser($"nokey-{Guid.NewGuid().ToString("N")[..10]}@uitest.local", UiFixture.Password, AppRoles.PropertyManager);

            new RegisterPage(browser).Open().Submit(user.Email, user.Password, role: user.Role);
            browser.WaitUntil(() => browser.PathAndQuery.StartsWith("/Identity/Account/RegisterConfirmation"), "the registration confirmation page");

            browser.WaitForText($"We couldn't send a confirmation email to {user.Email}.");
            Assert.Empty(app.Emails.To(user.Email));
            var link = browser.Driver.FindElement(By.LinkText("Confirm your account")).GetAttribute("href")!;

            browser.Driver.Navigate().GoToUrl(link);
            browser.WaitForText("Thank you for confirming your email.");
            browser.LogInAs(user);
            Assert.Contains("Review queue", new NavBar(browser).Links);
        }
    }
}
