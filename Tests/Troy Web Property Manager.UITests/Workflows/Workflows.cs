using Microsoft.EntityFrameworkCore;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.UITests.Infrastructure;
using Troy_Web_Property_Manager.UITests.Pages;

namespace Troy_Web_Property_Manager.UITests.Workflows
{
    /// <summary>A property and its unit numbers, created for one test.</summary>
    public sealed record Listing(int PropertyId, string Name, string Address, IReadOnlyList<string> Units)
    {
        public string FirstUnit => Units[0];
    }

    /// <summary>
    /// Signing up, logging in and out. Everything returns the browser (or a page) so steps chain:
    /// <c>browser.LogInAs(manager).CreateListing(...)</c>.
    /// </summary>
    public static class AccountWorkflows
    {
        /// <summary>A new browser already logged in as <paramref name="user"/> (closed again if the login fails).</summary>
        public static Browser NewBrowserAs(this UiFixture app, TestUser user)
        {
            var browser = app.NewBrowser();
            try
            {
                return browser.LogInAs(user);
            }
            catch
            {
                browser.Dispose();
                throw;
            }
        }

        /// <summary>Logs in through the login page and waits until the menu shows the user.</summary>
        public static Browser LogInAs(this Browser browser, TestUser user)
        {
            new LoginPage(browser).Open().Submit(user.Email, user.Password);
            browser.WaitUntil(() => new NavBar(browser).IsSignedIn, $"{user.Email} to be signed in");
            return browser;
        }

        public static Browser LogOut(this Browser browser)
        {
            new NavBar(browser).LogOut();
            return browser;
        }

        /// <summary>
        /// Signs up through the real Register page with <paramref name="role"/>, then follows the link from the
        /// confirmation email. Leaves the browser signed out, like a real new user.
        /// </summary>
        public static TestUser RegisterAndConfirm(this Browser browser, UiFixture app, string role)
        {
            var user = new TestUser($"signup-{Guid.NewGuid().ToString("N")[..10]}@uitest.local", UiFixture.Password, role);
            new RegisterPage(browser).Open().Submit(user.Email, user.Password, role: role);
            browser.WaitUntil(() => browser.PathAndQuery.StartsWith("/Identity/Account/RegisterConfirmation"), "the registration confirmation page");
            browser.Driver.Navigate().GoToUrl(app.Emails.ConfirmationLink(user.Email));
            browser.WaitForText("Thank you for confirming your email.");
            return user;
        }
    }

    /// <summary>What a property manager does: listings, claiming and reviewing.</summary>
    public static class ManagerWorkflows
    {
        /// <summary>Adds a property with these units through the Properties page modals.</summary>
        public static Listing CreateListing(this Browser browser, params UnitData[] units)
        {
            if (units.Length == 0) units = [new UnitData()];
            var page = new PropertiesPage(browser).Open();
            var name = UiFixture.Unique("UI Property");
            var address = "1 Test Way, Troy";
            page.AddProperty(name, address);
            foreach (var unit in units) page.AddUnit(name, unit);
            return new Listing(0, name, address, units.Select(u => u.Number!).ToList());
        }

        /// <summary>Opens the application and claims it for review (it goes Under Review).</summary>
        public static ApplicationPage Claim(this Browser browser, int applicationId)
        {
            var page = new ApplicationPage(browser).Open(applicationId);
            page.ClaimForReview();
            return page;
        }

        /// <summary>Claims (if it isn't already this manager's) and reviews with <paramref name="outcome"/>.</summary>
        public static ApplicationPage ReviewApplication(this Browser browser, int applicationId, string outcome, string? comment = null)
        {
            var page = new ApplicationPage(browser).Open(applicationId);
            if (page.HasButton("Claim for review")) page.ClaimForReview();
            page.Review(outcome, comment);
            return page;
        }

        public static ApplicationPage Approve(this Browser browser, int applicationId)
        {
            return browser.ReviewApplication(applicationId, "Approve");
        }
    }

    /// <summary>What an applicant does: apply, fill in the sections, submit.</summary>
    public static class ApplicantWorkflows
    {
        /// <summary>Clicks Apply on the Available units page; returns the application it opened.</summary>
        public static ApplicationPage ApplyFor(this Browser browser, Listing listing, string? unit = null)
        {
            return new UnitsPage(browser).Open().Apply(listing.Name, unit ?? listing.FirstUnit);
        }

        /// <summary>Fills and continues through section 1 and section 2 (one residence), ending on the Summary.</summary>
        public static ApplicationPage CompleteSections(this ApplicationPage page, ApplicantInfo? info = null, ResidenceData? residence = null)
        {
            page.FillApplicantInformation(info ?? ApplicantInfo.Valid());
            page.Continue();
            page.AddResidence(residence ?? ResidenceData.Valid());
            page.Continue();
            return page;
        }

        /// <summary>Apply, fill in both sections, submit. Returns the application id.</summary>
        public static int CreateSubmittedApplication(this Browser browser, Listing listing, string? unit = null)
        {
            var page = browser.ApplyFor(listing, unit).CompleteSections();
            page.Submit();
            return page.Id;
        }
    }

    /// <summary>
    /// Quick setup straight in the database, for tests that need a listing but aren't about managing properties
    /// (that has its own UI tests). Keeps the other tests fast and focused.
    /// </summary>
    public static class ArrangeWorkflows
    {
        public static Task<Listing> CreateListingAsync(this UiFixture app, params UnitData[] units)
        {
            if (units.Length == 0) units = [new UnitData()];
            return app.WithDbAsync(async db =>
            {
                var types = await db.UnitTypes.ToDictionaryAsync(t => t.Name);
                var property = new Property { Name = UiFixture.Unique("UI Listing"), Address = "2 Test Way, Troy" };
                foreach (var unit in units)
                {
                    property.Units.Add(new Unit
                    {
                        UnitNumber = unit.Number!,
                        Bedrooms = unit.Bedrooms ?? 1,
                        MonthlyRent = unit.Rent ?? 1000m,
                        UnitType = types[unit.Type ?? "Apartment"]
                    });
                }
                db.Properties.Add(property);
                await db.SaveChangesAsync();
                return new Listing(property.Id, property.Name, property.Address, units.Select(u => u.Number!).ToList());
            });
        }

        /// <summary>Gives an existing unit a (possibly inactive) unit type, which the UI can't do for new picks.</summary>
        public static Task SetUnitTypeAsync(this UiFixture app, Listing listing, string unit, string typeName)
        {
            return app.WithDbAsync(async db =>
            {
                var type = await db.UnitTypes.SingleAsync(t => t.Name == typeName);
                var row = await db.Units.SingleAsync(u => u.PropertyId == listing.PropertyId && u.UnitNumber == unit);
                row.UnitTypeId = type.Id;
                await db.SaveChangesAsync();
            });
        }

        public static Task<int> UnitTypeIdAsync(this UiFixture app, string typeName)
        {
            return app.WithDbAsync(db => db.UnitTypes.Where(t => t.Name == typeName).Select(t => t.Id).SingleAsync());
        }
    }
}
