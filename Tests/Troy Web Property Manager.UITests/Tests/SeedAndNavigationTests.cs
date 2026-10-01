using Microsoft.EntityFrameworkCore;
using Troy_Web_Property_Manager.Data;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.UITests.Infrastructure;
using Troy_Web_Property_Manager.UITests.Pages;
using Troy_Web_Property_Manager.UITests.Workflows;

namespace Troy_Web_Property_Manager.UITests.Tests
{
    /// <summary>
    /// Technical 2.b: on start the database is created, migrated and seeded with lookups, property managers, applicants,
    /// properties, units and applications in every status (the UI tests run against exactly that: a brand new database
    /// that Program.cs creates and seeds).
    /// </summary>
    [Collection(UiCollection.Name)]
    public sealed class SeedDataTests(UiFixture app)
    {
        [Theory]
        [InlineData("Submitted")]
        [InlineData("Under Review")]
        [InlineData("Returned")]
        [InlineData("Approved")]
        [InlineData("Denied")]
        [InlineData("Withdrawn")]
        public void Manager_SeesSeededApplicationsInEveryReviewedStatus(string status)
        {
            using var manager = app.NewBrowserAs(UiFixture.SeededManager);
            var list = new ApplicationListPage(manager).Open();

            list.Filter(status: status);

            Assert.NotEmpty(list.Rows);
            Assert.All(list.Rows, r => Assert.Equal(status, r.Status));
        }

        [Fact]
        public async Task SeededDrafts_ShowUpForTheirApplicant()
        {
            // Managers never see a draft that was never submitted, so find a seeded applicant who has one.
            var email = await app.WithDbAsync(db =>
                (from a in db.RentalApplications
                 where a.Status == (long)ApplicationStatus.Draft && a.Submitted == null
                 join u in db.Users on a.Applicant.UserId equals u.Id
                 where u.Email!.EndsWith("@example.com")
                 select u.Email).FirstAsync());
            using var applicant = app.NewBrowserAs(new TestUser(email!, DemoDataSeeder.Password, AppRoles.Applicant));
            var list = new ApplicationListPage(applicant).Open();

            list.Filter(status: "Draft");

            Assert.NotEmpty(list.Rows);
        }

        [Fact]
        public void SeededPropertiesAndUnits_AreListed_WithTheSeededUnitTypes()
        {
            using var manager = app.NewBrowserAs(UiFixture.SeededManager);
            var properties = new PropertiesPage(manager).Open();

            Assert.True(manager.FindAll(OpenQA.Selenium.By.CssSelector("div.card[id^='property-']")).Count >= 6);
            properties.OpenAddUnit(manager.Text(OpenQA.Selenium.By.CssSelector("div.card[id^='property-'] .card-header strong")));
            Assert.Equal(["Choose…", "Apartment", "Studio", "Townhouse"], properties.UnitTypeOptions);
        }
    }

    /// <summary>The home page and menu: each role gets its own links, and the current page is highlighted.</summary>
    [Collection(UiCollection.Name)]
    public sealed class NavigationTests(UiFixture app)
    {
        [Fact]
        public void HomePage_ForAVisitor_OffersLogInAndRegister()
        {
            using var browser = app.NewBrowser();
            browser.Go("/");

            Assert.Contains("Log in or register to get started.", browser.PageText);
            Assert.Equal(["Home", "Privacy"], new NavBar(browser).Links);
        }

        [Theory]
        [InlineData(true, "/Properties", "Properties")]
        [InlineData(true, "/Applications", "Applications")]
        [InlineData(true, "/Applications/Queue", "Review queue")]
        [InlineData(false, "/Units", "Available units")]
        [InlineData(false, "/Applications", "My applications")]
        [InlineData(false, "/", "Home")]
        public async Task CurrentPage_IsHighlightedInTheMenu(bool manager, string path, string expected)
        {
            using var browser = app.NewBrowserAs(manager ? await app.CreateManagerAsync() : await app.CreateApplicantAsync());

            browser.Go(path);

            Assert.Equal(expected, new NavBar(browser).Active);
        }

        [Fact]
        public async Task HomePage_LinksToEachRolesPages()
        {
            using var manager = app.NewBrowserAs(await app.CreateManagerAsync());
            using var applicant = app.NewBrowserAs(await app.CreateApplicantAsync());
            manager.Go("/");
            applicant.Go("/");

            Assert.Contains("Manage your properties and review applications.", manager.PageText);
            Assert.Contains("Browse available units or check on your applications.", applicant.PageText);
        }
    }
}
