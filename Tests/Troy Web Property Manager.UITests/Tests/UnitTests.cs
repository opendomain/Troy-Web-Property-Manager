using OpenQA.Selenium;
using Troy_Web_Property_Manager.UITests.Infrastructure;
using Troy_Web_Property_Manager.UITests.Pages;
using Troy_Web_Property_Manager.UITests.Workflows;

namespace Troy_Web_Property_Manager.UITests.Tests
{
    /// <summary>
    /// Functional 2.c: Unit Type is a lookup with active and inactive values. An inactive type still shows on a unit
    /// that already has it, but can't be picked for any other unit - enforced on the server, so these tests also post
    /// an inactive type the dropdown never offered.
    /// </summary>
    [Collection(UiCollection.Name)]
    public sealed class UnitTypeTests(UiFixture app)
    {
        [Fact]
        public async Task InactiveType_StillShowsOnTheUnitThatHasIt()
        {
            var listing = await app.CreateListingAsync(new UnitData("101"));
            await app.SetUnitTypeAsync(listing, "101", "Loft");
            using var browser = app.NewBrowserAs(await app.CreateManagerAsync());

            var page = new PropertiesPage(browser).Open();

            Assert.Equal("Loft (inactive)", page.Unit(listing.Name, "101").Type);
        }

        [Fact]
        public async Task NewUnit_OnlyOffersActiveTypes()
        {
            var listing = await app.CreateListingAsync();
            using var browser = app.NewBrowserAs(await app.CreateManagerAsync());
            var page = new PropertiesPage(browser).Open();

            page.OpenAddUnit(listing.Name);

            Assert.Equal(["Choose…", "Apartment", "Studio", "Townhouse"], page.UnitTypeOptions);
        }

        [Fact]
        public async Task EditingAUnitWithAnInactiveType_OffersAndKeepsIt()
        {
            var listing = await app.CreateListingAsync(new UnitData("101"));
            await app.SetUnitTypeAsync(listing, "101", "Loft");
            using var browser = app.NewBrowserAs(await app.CreateManagerAsync());
            var page = new PropertiesPage(browser).Open();

            page.OpenEditUnit(listing.Name, "101");
            Assert.Contains("Loft (inactive)", page.UnitTypeOptions);
            Assert.Equal("Loft (inactive)", page.SelectedUnitType);
            page.FillUnitModal(new UnitData(null, null, 1234m, null));
            browser.WaitForModalClosed();

            browser.WaitUntil(() => page.Unit(listing.Name, "101").Rent == "$1,234.00", "the saved rent");
            Assert.Equal("Loft (inactive)", page.Unit(listing.Name, "101").Type);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PickingAnInactiveTypeByTamperingWithTheForm_IsRejectedByTheServer(bool editExisting)
        {
            var listing = await app.CreateListingAsync(new UnitData("101", Type: "Apartment"));
            var loftId = await app.UnitTypeIdAsync("Loft");
            using var browser = app.NewBrowserAs(await app.CreateManagerAsync());
            var page = new PropertiesPage(browser).Open();

            if (editExisting) page.OpenEditUnit(listing.Name, "101");
            else page.OpenAddUnit(listing.Name);
            // Put the inactive type into the dropdown and pick it - the page never offers it.
            browser.Js("const s = document.querySelector('#app-modal #UnitTypeId'); s.add(new Option('Loft', arguments[0])); s.value = arguments[0];",
                loftId.ToString());
            browser.SetValue(By.CssSelector("#app-modal #UnitNumber"), editExisting ? "101" : "102");
            browser.SetValue(By.CssSelector("#app-modal #Bedrooms"), "1");
            browser.SetValue(By.CssSelector("#app-modal #MonthlyRent"), "1100");
            browser.ClickModalButton("Save");

            browser.WaitForModalText("Choose an active unit type.");
            page.Open();
            Assert.Equal("Apartment", page.Unit(listing.Name, "101").Type);
            Assert.False(page.HasUnit(listing.Name, "102"));
        }
    }

    /// <summary>
    /// Functional 2.b and 2.d: applicants browse the available units (filter and sort them) and start an application
    /// for one; a unit whose lease covers today isn't available.
    /// </summary>
    [Collection(UiCollection.Name)]
    public sealed class AvailableUnitsTests(UiFixture app)
    {
        private Task<Listing> ThreeUnitsAsync()
        {
            return app.CreateListingAsync(
                new UnitData("101", 1, 1200m, "Studio"),
                new UnitData("102", 3, 2100m, "Apartment"),
                new UnitData("103", 2, 1500m, "Townhouse"));
        }

        [Fact]
        public async Task Applicant_SeesTheAvailableUnits_WithTheirDetails()
        {
            var listing = await ThreeUnitsAsync();
            using var browser = app.NewBrowserAs(await app.CreateApplicantAsync());

            var rows = new UnitsPage(browser).Open().RowsFor(listing.Name);

            Assert.Equal(
            [
                new AvailableUnitRow(listing.Name, "101", 1, "$1,200.00", "Studio"),
                new AvailableUnitRow(listing.Name, "102", 3, "$2,100.00", "Apartment"),
                new AvailableUnitRow(listing.Name, "103", 2, "$1,500.00", "Townhouse")
            ], rows);
        }

        [Fact]
        public async Task Filters_ByPropertyAndMinimumBedrooms()
        {
            var listing = await ThreeUnitsAsync();
            using var browser = app.NewBrowserAs(await app.CreateApplicantAsync());
            var page = new UnitsPage(browser).Open();

            page.Filter(property: listing.Name, bedrooms: "2+");

            Assert.All(page.Rows, r => Assert.Equal(listing.Name, r.Property));
            Assert.Equal(["102", "103"], page.Rows.Select(r => r.Unit));
            browser.ClickAndWaitForPage(By.XPath(Xp.Button("Clear")));
            Assert.True(page.Rows.Select(r => r.Property).Distinct().Count() > 1, "Clear should show every property again.");
        }

        [Theory]
        [InlineData("Rent", "ascending", new[] { "101", "103", "102" })]
        [InlineData("Bedrooms", "ascending", new[] { "101", "103", "102" })]
        [InlineData("Type", "ascending", new[] { "102", "101", "103" })]
        public async Task ColumnHeaders_SortTheList_AndASecondClickReverses(string header, string state, string[] expectedUnits)
        {
            var listing = await ThreeUnitsAsync();
            using var browser = app.NewBrowserAs(await app.CreateApplicantAsync());
            var page = new UnitsPage(browser).Open($"?propertyId={listing.PropertyId}");

            page.SortBy(header);
            Assert.Equal(state, page.SortState(header));
            Assert.Equal(expectedUnits, page.Rows.Select(r => r.Unit));

            page.SortBy(header);
            Assert.Equal("descending", page.SortState(header));
            Assert.Equal(expectedUnits.Reverse(), page.Rows.Select(r => r.Unit));
            Assert.Contains($"propertyId={listing.PropertyId}", browser.PathAndQuery);
        }

        [Fact]
        public async Task Apply_StartsADraftOnSection1_AndApplyingAgainReopensIt()
        {
            var listing = await app.CreateListingAsync();
            var applicant = await app.CreateApplicantAsync();
            using var browser = app.NewBrowserAs(applicant);

            var first = browser.ApplyFor(listing);
            var id = first.Id;
            Assert.Equal("Draft", first.Status);
            Assert.Equal("Applicant information", first.Section);
            // Section 1 starts filled from the applicant's profile (just their login email for a new account).
            Assert.Equal(applicant.Email, first.ReadApplicantInformation().Email);

            Assert.Equal(id, browser.ApplyFor(listing).Id);
        }
    }
}
