using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.UITests.Infrastructure;
using Troy_Web_Property_Manager.UITests.Pages;

namespace Troy_Web_Property_Manager.UITests.Tests;

/// <summary>The Development shortcuts are keyed on "is Development", so Staging has to behave like Production.</summary>
[Collection(StagingUiCollection.Name)]
public sealed class StagingEnvironmentTests(StagingUiFixture app)
{
    [Fact]
    public async Task Registration_IsApplicantOnly_AndNeedsTheEmailedLink()
    {
        using var browser = app.NewBrowser();
        var register = new RegisterPage(browser).Open();
        Assert.Empty(browser.Driver.FindElements(By.Id("role-PropertyManager")));
        Assert.True(browser.Driver.FindElement(By.Id("role-Applicant")).Selected);

        // No role passed: the preselected Applicant is what gets posted.
        var email = $"staging-{Guid.NewGuid():N}@uitest.local";
        register.Submit(email, UiFixture.Password);
        browser.WaitUntil(() => browser.PathAndQuery.StartsWith("/Identity/Account/RegisterConfirmation"), "confirmation page");

        // ShowConfirmationLink is on in this fixture, and still no link.
        Assert.Empty(browser.Driver.FindElements(By.LinkText("Confirm your account")));
        Assert.DoesNotContain("/Identity/Account/ConfirmEmail?", browser.Driver.PageSource);
        Assert.False(await app.WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.EmailConfirmed).SingleAsync()));
        Assert.True(await app.WithDbAsync(db => db.UserRoles.AnyAsync(ur =>
            ur.UserId == db.Users.Single(u => u.Email == email).Id &&
            ur.RoleId == db.Roles.Single(r => r.Name == AppRoles.Applicant).Id)));
    }

    [Fact]
    public async Task Startup_DoesNotSeedDemoData()
    {
        Assert.False(await app.WithDbAsync(db => db.Users.AnyAsync(u => u.Email == UiFixture.SeededManager.Email)));
        Assert.False(await app.WithDbAsync(db => db.Properties.AnyAsync()));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Identity/Account/Register")]
    [InlineData("/Identity/Account/Login")]
    [InlineData("/Identity/Account/ForgotPassword")]
    [InlineData("/Identity/Account/ResendEmailConfirmation")]
    public async Task PageStylesAndScripts_AllLoad(string path)
    {
        // Includes the scoped CSS bundle and the Identity UI's validation scripts, which outside Development only
        // load from the build output if the app enables static web assets.
        var broken = await app.BrokenAssetsAsync(path);
        Assert.True(broken.Count == 0, "Not loading: " + string.Join(", ", broken));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Privacy")]
    public async Task PageCounter_IsOff_EvenWhenConfigured(string path)
    {
        using var http = new HttpClient();
        var html = await http.GetStringAsync(app.BaseUrl + path);
        Assert.DoesNotContain("gc.zgo.at/count.js", html);
        Assert.DoesNotContain("data-goatcounter", html);
    }
}
