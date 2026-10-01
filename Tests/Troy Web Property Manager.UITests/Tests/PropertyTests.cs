using Troy_Web_Property_Manager.UITests.Infrastructure;
using Troy_Web_Property_Manager.UITests.Pages;
using Troy_Web_Property_Manager.UITests.Workflows;

namespace Troy_Web_Property_Manager.UITests.Tests
{
    /// <summary>
    /// Functional 2.b: a property manager adds, edits and removes properties and their units (unit number, bedrooms,
    /// monthly rent, unit type) through modals. Technical 1.b: the modal is a partial from the controller; if it
    /// doesn't validate the same partial comes back with the messages and redraws in place; if it works the modal
    /// closes and only the affected part of the page refreshes (checked with a marker that a page reload would lose).
    /// </summary>
    [Collection(UiCollection.Name)]
    public sealed class PropertyTests(UiFixture app)
    {
        private async Task<(Browser Browser, PropertiesPage Page)> ManagerOnPropertiesAsync()
        {
            var browser = app.NewBrowserAs(await app.CreateManagerAsync());
            return (browser, new PropertiesPage(browser).Open());
        }

        [Fact]
        public async Task AddProperty_ThroughTheModal_AddsItsCard_WithoutReloadingThePage()
        {
            var (browser, page) = await ManagerOnPropertiesAsync();
            using var _ = browser;
            var name = UiFixture.Unique("Maple Court");
            browser.MarkPage();

            page.AddProperty(name, "12 Maple St, Troy");

            Assert.True(browser.IsSamePage(), "The page reloaded instead of refreshing the property list.");
            Assert.Contains("12 Maple St, Troy", page.CardText(name));
            Assert.True(page.Open().HasProperty(name), "The property wasn't saved.");
        }

        [Fact]
        public async Task AddProperty_WithMissingFields_ShowsTheErrorsInTheModal_AndSavesNothing()
        {
            var (browser, page) = await ManagerOnPropertiesAsync();
            using var _ = browser;

            page.OpenAddProperty();
            page.FillPropertyModal("", "");

            browser.WaitForModalText("The Name field is required.");
            Assert.Contains("The Address field is required.", browser.ModalText);
            Assert.True(browser.IsModalOpen());
        }

        [Fact]
        public async Task AddProperty_InvalidPostReachesTheServer_TheSamePartialComesBackWithErrors()
        {
            var (browser, page) = await ManagerOnPropertiesAsync();
            using var _ = browser;
            page.OpenAddProperty();
            browser.MarkPage();

            // Skip the browser's checks so the server's validation (a 422 with the same partial) is what we see.
            browser.DisableModalClientValidation();
            page.FillPropertyModal("", "");

            browser.WaitForModalText("The Name field is required.");
            Assert.Contains("The Address field is required.", browser.ModalText);
            Assert.Equal("Add property", browser.ModalTitle());
            Assert.True(browser.IsSamePage());
        }

        [Fact]
        public async Task EditProperty_UpdatesOnlyThatCard()
        {
            var (browser, page) = await ManagerOnPropertiesAsync();
            using var _ = browser;
            var first = UiFixture.Unique("First");
            var second = UiFixture.Unique("Second");
            page.AddProperty(first, "1 First St");
            page.AddProperty(second, "2 Second St");
            browser.MarkPage();

            var renamed = UiFixture.Unique("Renamed");
            page.RenameProperty(first, renamed);

            Assert.True(browser.IsSamePage());
            Assert.False(page.HasProperty(first));
            Assert.True(page.HasProperty(second));
            Assert.True(page.Open().HasProperty(renamed));
        }

        [Fact]
        public async Task RemoveProperty_WithNoApplications_RemovesItsCard()
        {
            var (browser, page) = await ManagerOnPropertiesAsync();
            using var _ = browser;
            var name = UiFixture.Unique("Short Lived");
            page.AddProperty(name, "3 Gone St");
            page.AddUnit(name, new UnitData("101"));

            page.RemoveProperty(name);

            Assert.False(page.Open().HasProperty(name));
        }

        [Fact]
        public async Task RemoveProperty_WhenAUnitHasApplications_IsRefusedInTheModal()
        {
            var listing = await app.CreateListingAsync();
            using (var applicant = app.NewBrowserAs(await app.CreateApplicantAsync())) applicant.ApplyFor(listing);
            var (browser, page) = await ManagerOnPropertiesAsync();
            using var _ = browser;

            page.OpenRemoveProperty(listing.Name);
            browser.ClickModalButton(PropertiesPage.ConfirmButton);

            browser.WaitForModalText("This property has units with applications, so it can't be removed.");
            Assert.True(page.Open().HasProperty(listing.Name));
        }

        [Fact]
        public async Task AddEditAndRemoveAUnit_ThroughTheModal()
        {
            var (browser, page) = await ManagerOnPropertiesAsync();
            using var _ = browser;
            var name = UiFixture.Unique("Unit Works");
            page.AddProperty(name, "4 Unit Rd");
            browser.MarkPage();

            page.AddUnit(name, new UnitData("101", 2, 1450m, "Apartment"));
            Assert.Equal(new UnitRow("101", "2", "$1,450.00", "Apartment", "Available"), page.Unit(name, "101"));

            page.EditUnit(name, "101", new UnitData(null, 3, 1600m, "Townhouse"));
            browser.WaitUntil(() => page.Unit(name, "101").Rent == "$1,600.00", "the edited rent");
            Assert.Equal(new UnitRow("101", "3", "$1,600.00", "Townhouse", "Available"), page.Unit(name, "101"));

            page.RemoveUnit(name, "101");
            Assert.True(browser.IsSamePage(), "A unit change reloaded the page instead of redrawing the card.");
            Assert.False(page.Open().HasUnit(name, "101"));
        }

        [Theory]
        [InlineData("", "2", "1500", "The Unit number field is required.")]
        [InlineData("101", "11", "1500", "The field Bedrooms must be between 0 and 10.")]
        [InlineData("101", "2", "0", "The field Monthly rent must be between 1 and 100000.")]
        public async Task AddUnit_WithInvalidValues_ShowsTheErrorInTheModal(string number, string bedrooms, string rent, string expected)
        {
            var (browser, page) = await ManagerOnPropertiesAsync();
            using var _ = browser;
            var name = UiFixture.Unique("Bad Units");
            page.AddProperty(name, "5 Error Ave");

            page.OpenAddUnit(name);
            browser.SetValue(OpenQA.Selenium.By.CssSelector("#app-modal #UnitNumber"), number);
            browser.SetValue(OpenQA.Selenium.By.CssSelector("#app-modal #Bedrooms"), bedrooms);
            browser.SetValue(OpenQA.Selenium.By.CssSelector("#app-modal #MonthlyRent"), rent);
            browser.SelectByText(OpenQA.Selenium.By.CssSelector("#app-modal #UnitTypeId"), "Apartment");
            browser.ClickModalButton("Save");

            browser.WaitForModalText(expected);
            Assert.False(page.Open().HasUnit(name, "101"));
        }

        [Fact]
        public async Task AddUnit_WithoutAUnitType_IsRejected()
        {
            var (browser, page) = await ManagerOnPropertiesAsync();
            using var _ = browser;
            var name = UiFixture.Unique("No Type");
            page.AddProperty(name, "6 Type Ln");

            page.OpenAddUnit(name);
            browser.DisableModalClientValidation();
            page.FillUnitModal(new UnitData("101", 1, 900m, Type: null));

            browser.WaitForModalText("The Unit type field is required.");
        }

        [Fact]
        public async Task AddUnit_WithANumberThePropertyAlreadyHas_IsRejectedByTheServer()
        {
            var (browser, page) = await ManagerOnPropertiesAsync();
            using var _ = browser;
            var name = UiFixture.Unique("Dupes");
            page.AddProperty(name, "7 Twin Rd");
            page.AddUnit(name, new UnitData("101"));

            page.OpenAddUnit(name);
            page.FillUnitModal(new UnitData("101", 1, 999m, "Studio"));

            browser.WaitForModalText("This unit number already exists at the property.");
            Assert.Equal("2", page.Open().Unit(name, "101").Bedrooms);
        }

        [Fact]
        public async Task RemoveUnit_WithApplications_IsRefusedInTheModal()
        {
            var listing = await app.CreateListingAsync();
            using (var applicant = app.NewBrowserAs(await app.CreateApplicantAsync())) applicant.ApplyFor(listing);
            var (browser, page) = await ManagerOnPropertiesAsync();
            using var _ = browser;

            page.OpenRemoveUnit(listing.Name, listing.FirstUnit);
            browser.ClickModalButton(PropertiesPage.ConfirmButton);

            browser.WaitForModalText("This unit has applications, so it can't be removed.");
            Assert.True(page.Open().HasUnit(listing.Name, listing.FirstUnit));
        }
    }
}
