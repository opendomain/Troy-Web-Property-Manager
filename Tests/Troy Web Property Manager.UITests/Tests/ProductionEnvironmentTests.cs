using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using Troy_Web_Property_Manager.Data;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.UITests.Infrastructure;
using Troy_Web_Property_Manager.UITests.Pages;
using Troy_Web_Property_Manager.UITests.Workflows;

namespace Troy_Web_Property_Manager.UITests.Tests;

[Collection(ProductionUiCollection.Name)]
public sealed class ProductionEnvironmentTests(ProductionUiFixture app)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registration_RequiresEmailConfirmation_EvenWhenShowConfirmationLinkIsEnabled(bool sendingFails)
    {
        using var browser = app.NewBrowser();
        var email = $"production-{Guid.NewGuid():N}@uitest.local";
        if (sendingFails) app.Emails.FailFor(email);

        new RegisterPage(browser).Open().Submit(email, UiFixture.Password, role: AppRoles.Applicant);
        browser.WaitUntil(() => browser.PathAndQuery.StartsWith("/Identity/Account/RegisterConfirmation"), "confirmation page");
        Assert.Empty(browser.Driver.FindElements(By.LinkText("Confirm your account")));
        Assert.DoesNotContain("/Identity/Account/ConfirmEmail?", browser.Driver.PageSource);
        Assert.False(await app.WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.EmailConfirmed).SingleAsync()));
        if (sendingFails)
        {
            browser.WaitForText($"We couldn't send a confirmation email to {email}.");
            Assert.Single(browser.Driver.FindElements(By.LinkText("Resend confirmation email")));
            Assert.Empty(app.Emails.To(email));
        }

        var login = new LoginPage(browser).Open();
        login.Submit(email, UiFixture.Password);
        Assert.Contains("You must confirm your email before you can log in.", login.Errors);
        Assert.False(new NavBar(browser).IsSignedIn);

        if (!sendingFails)
        {
            browser.Driver.Navigate().GoToUrl(app.Emails.ConfirmationLink(email));
            browser.WaitForText("Thank you for confirming your email.");
            browser.LogInAs(new TestUser(email, UiFixture.Password, AppRoles.Applicant));
            Assert.Contains("My applications", new NavBar(browser).Links);
        }
    }

    [Fact]
    public async Task Registration_RejectsPropertyManagerRole_IncludingTamperedPosts()
    {
        using var browser = app.NewBrowser();
        var register = new RegisterPage(browser).Open();
        Assert.Empty(browser.Driver.FindElements(By.Id("role-PropertyManager")));
        browser.Js("document.getElementById('role-Applicant').value = 'Property Manager';");
        var email = $"manager-tamper-{Guid.NewGuid():N}@uitest.local";
        register.SubmitSkippingBrowserValidation(email, UiFixture.Password, UiFixture.Password, AppRoles.Applicant);
        Assert.Contains("Please select a valid role.", register.Errors);
        Assert.False(await app.WithDbAsync(db => db.Users.AnyAsync(u => u.Email == email)));
    }

    [Fact]
    public async Task Startup_SeedsRequiredDataWithoutDemoAccountsOrProperties()
    {
        Assert.False(await app.WithDbAsync(db => db.Users.AnyAsync(u => u.Email == UiFixture.SeededManager.Email)));
        Assert.False(await app.WithDbAsync(db => db.Properties.AnyAsync()));
        Assert.Equal(AppRoles.All.Length, await app.WithDbAsync(db => db.Roles.CountAsync()));
        Assert.Equal(Enum.GetValues<ApplicationStatus>().Length, await app.WithDbAsync(db => db.Statuses.CountAsync()));
        Assert.Equal(4, await app.WithDbAsync(db => db.UnitTypes.CountAsync()));
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/ApplyMigrations")]
    public async Task DeveloperEndpoints_AreUnavailable(string path)
    {
        using var http = new HttpClient();
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(app.BaseUrl + path)).StatusCode);
    }

    [Fact]
    public async Task ErrorPage_DoesNotShowDevelopmentInstructions()
    {
        using var http = new HttpClient();
        var html = await http.GetStringAsync(app.BaseUrl + "/Error");
        Assert.DoesNotContain("Development Mode", html);
        Assert.DoesNotContain("ASPNETCORE_ENVIRONMENT", html);
    }

    [Fact]
    public async Task Startup_WithUndeployedMigrations_RefusesToCreateProductionDatabase()
    {
        await using var factory = new AppFactory(environment: "Production");
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(factory.ConnectionString).Options);
        try
        {
            factory.UseKestrel(0);
            var error = Assert.Throws<InvalidOperationException>(() => factory.StartServer());
            Assert.Contains("pending migrations", error.Message);
            Assert.False(await db.Database.CanConnectAsync());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Startup_WithoutConnectionString_FailsWithAClearError()
    {
        // Production has no LocalDB fallback, so a missing setting has to stop the app rather than pick a database.
        await using var factory = new AppFactory(environment: "Production")
            .WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:DefaultConnection", ""));

        var error = Assert.Throws<InvalidOperationException>(() => factory.Server);
        Assert.Contains("ConnectionStrings:DefaultConnection", error.Message);
    }
}

[Collection(ProductionNoEmailUiCollection.Name)]
public sealed class ProductionNoEmailTests(ProductionNoEmailUiFixture app)
{
    [Fact]
    public async Task MissingEmailKey_DoesNotExposeConfirmationLinkOrPermitLogin()
    {
        using var browser = app.NewBrowser();
        var email = $"production-nokey-{Guid.NewGuid():N}@uitest.local";
        new RegisterPage(browser).Open().Submit(email, UiFixture.Password, role: AppRoles.Applicant);
        browser.WaitForText($"We couldn't send a confirmation email to {email}.");
        Assert.Empty(browser.Driver.FindElements(By.LinkText("Confirm your account")));
        Assert.DoesNotContain("/Identity/Account/ConfirmEmail?", browser.Driver.PageSource);
        Assert.Empty(app.Emails.To(email));
        Assert.False(await app.WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.EmailConfirmed).SingleAsync()));
        var login = new LoginPage(browser).Open();
        login.Submit(email, UiFixture.Password);
        Assert.Contains("You must confirm your email before you can log in.", login.Errors);
        Assert.False(new NavBar(browser).IsSignedIn);
    }
}

[Collection(DevelopmentTelemetryUiCollection.Name)]
public sealed class DevelopmentTelemetryTests(DevelopmentTelemetryUiFixture app)
{
    [Theory]
    [InlineData("/")]
    [InlineData("/Privacy")]
    public async Task ConfiguredCounter_RunsInDevelopment_WithLocalhostSupport(string path)
    {
        using var http = new HttpClient();
        var html = await http.GetStringAsync(app.BaseUrl + path);
        Assert.Contains("gc.zgo.at/count.js", html);
        Assert.Contains($"data-goatcounter=\"{TelemetryUiFixture.GoatCounterUrl}\"", html);
        Assert.Contains("allow_local", html);
    }

    [Theory]
    [InlineData("/Identity/Account/Register")]
    [InlineData("/Identity/Account/Login")]
    [InlineData("/Identity/Account/RegisterConfirmation?email=someone%40uitest.local")]
    [InlineData("/Identity/Account/ForgotPassword")]
    [InlineData("/Identity/Account/ResetPassword?code=not-a-real-token")]
    [InlineData("/Identity/Account/ResendEmailConfirmation")]
    public async Task IdentityPages_ExcludeTheCounter_InDevelopment(string path)
    {
        using var http = new HttpClient();
        var html = await http.GetStringAsync(app.BaseUrl + path);
        Assert.DoesNotContain("gc.zgo.at/count.js", html);
        Assert.DoesNotContain("data-goatcounter", html);
    }
}
