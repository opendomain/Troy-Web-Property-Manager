using OpenQA.Selenium;
using Troy_Web_Property_Manager.UITests.Infrastructure;

namespace Troy_Web_Property_Manager.UITests.Pages
{
    /// <summary>What goes into the unit modal. Null fields are left as they are (or blank on a new unit).</summary>
    public sealed record UnitData(string? Number = "101", int? Bedrooms = 2, decimal? Rent = 1500m, string? Type = "Apartment");

    /// <summary>One unit row on a property card, as displayed.</summary>
    public sealed record UnitRow(string Number, string Bedrooms, string Rent, string Type, string Status);

    /// <summary>
    /// The managers' Properties page (2.b): one card per property with its units, and every add/edit/remove through
    /// the shared modal. The "Open…" methods only open a modal, so a test can fill it with bad values and check the
    /// errors; the plain methods do the whole thing and wait for the modal to close.
    /// </summary>
    public sealed class PropertiesPage(Browser browser)
    {
        public const string Path = "/Properties";

        /// <summary>The button in the "Remove property" / "Remove unit" confirmations (the shared dialog's default).</summary>
        public const string ConfirmButton = "Confirm";

        public PropertiesPage Open()
        {
            browser.Go(Path);
            return this;
        }

        private static string CardXPath(string propertyName)
        {
            return $"//div[contains(@class,'card') and starts-with(@id,'property-')][.//div[contains(@class,'card-header')]//strong[normalize-space()={Xp.Literal(propertyName)}]]";
        }

        private static string UnitRowXPath(string propertyName, string unitNumber)
        {
            return $"{CardXPath(propertyName)}//tbody/tr[td[1][normalize-space()={Xp.Literal(unitNumber)}]]";
        }

        public bool HasProperty(string name)
        {
            return browser.IsVisible(By.XPath(CardXPath(name)));
        }

        public string CardText(string propertyName)
        {
            return browser.Text(By.XPath(CardXPath(propertyName)));
        }

        public void WaitForProperty(string name)
        {
            browser.WaitUntil(() => HasProperty(name), $"the '{name}' card");
        }

        public void WaitForNoProperty(string name)
        {
            browser.WaitUntil(() => !HasProperty(name), $"the '{name}' card to go away");
        }

        // ---------------- Properties ----------------

        public void OpenAddProperty()
        {
            browser.Click(By.XPath(Xp.Button("Add property")));
            browser.WaitForModal("Add property");
        }

        public void OpenEditProperty(string name)
        {
            browser.Click(By.XPath($"{CardXPath(name)}//div[contains(@class,'card-header')]{Xp.Button("Edit")}"));
            browser.WaitForModal("Edit property");
        }

        /// <summary>Fills the open property modal and clicks Save. Null leaves a field as it is.</summary>
        public void FillPropertyModal(string? name, string? address)
        {
            if (name is not null) browser.SetValue(By.CssSelector("#app-modal #Name"), name);
            if (address is not null) browser.SetValue(By.CssSelector("#app-modal #Address"), address);
            browser.ClickModalButton("Save");
        }

        public void AddProperty(string name, string address)
        {
            OpenAddProperty();
            FillPropertyModal(name, address);
            browser.WaitForModalClosed();
            WaitForProperty(name);
        }

        public void RenameProperty(string name, string newName)
        {
            OpenEditProperty(name);
            FillPropertyModal(newName, null);
            browser.WaitForModalClosed();
            WaitForProperty(newName);
        }

        public void OpenRemoveProperty(string name)
        {
            browser.Click(By.XPath($"{CardXPath(name)}//div[contains(@class,'card-header')]{Xp.Button("Remove")}"));
            browser.WaitForModal("Remove property");
        }

        public void RemoveProperty(string name)
        {
            OpenRemoveProperty(name);
            browser.ClickModalButton(ConfirmButton);
            browser.WaitForModalClosed();
            WaitForNoProperty(name);
        }

        // ---------------- Units ----------------

        public void OpenAddUnit(string propertyName)
        {
            browser.Click(By.XPath($"{CardXPath(propertyName)}{Xp.Button("Add unit")}"));
            browser.WaitForModal("Add unit");
        }

        public void OpenEditUnit(string propertyName, string unitNumber)
        {
            browser.Click(By.XPath($"{UnitRowXPath(propertyName, unitNumber)}{Xp.Button("Edit")}"));
            browser.WaitForModal("Edit unit");
        }

        /// <summary>The unit type dropdown's options in the open unit modal.</summary>
        public IReadOnlyList<string> UnitTypeOptions => browser.Options(By.CssSelector("#app-modal #UnitTypeId"));

        public string SelectedUnitType =>
            browser.Js("const s = document.querySelector('#app-modal #UnitTypeId'); return s.options[s.selectedIndex].text;") as string ?? "";

        /// <summary>Fills the open unit modal and clicks Save. Null fields are left as they are.</summary>
        public void FillUnitModal(UnitData unit)
        {
            if (unit.Number is not null) browser.SetValue(By.CssSelector("#app-modal #UnitNumber"), unit.Number);
            if (unit.Bedrooms is not null) browser.SetValue(By.CssSelector("#app-modal #Bedrooms"), unit.Bedrooms.Value.ToString());
            if (unit.Rent is not null) browser.SetValue(By.CssSelector("#app-modal #MonthlyRent"), unit.Rent.Value.ToString("0.##"));
            if (unit.Type is not null) browser.SelectByText(By.CssSelector("#app-modal #UnitTypeId"), unit.Type);
            browser.ClickModalButton("Save");
        }

        public void AddUnit(string propertyName, UnitData unit)
        {
            OpenAddUnit(propertyName);
            FillUnitModal(unit);
            browser.WaitForModalClosed();
            browser.WaitUntil(() => HasUnit(propertyName, unit.Number!), $"unit {unit.Number} on '{propertyName}'");
        }

        public void EditUnit(string propertyName, string unitNumber, UnitData changes)
        {
            OpenEditUnit(propertyName, unitNumber);
            FillUnitModal(changes);
            browser.WaitForModalClosed();
        }

        public void OpenRemoveUnit(string propertyName, string unitNumber)
        {
            browser.Click(By.XPath($"{UnitRowXPath(propertyName, unitNumber)}{Xp.Button("Remove")}"));
            browser.WaitForModal("Remove unit");
        }

        public void RemoveUnit(string propertyName, string unitNumber)
        {
            OpenRemoveUnit(propertyName, unitNumber);
            browser.ClickModalButton(ConfirmButton);
            browser.WaitForModalClosed();
            browser.WaitUntil(() => !HasUnit(propertyName, unitNumber), $"unit {unitNumber} to go away");
        }

        public bool HasUnit(string propertyName, string unitNumber)
        {
            return browser.IsVisible(By.XPath(UnitRowXPath(propertyName, unitNumber)));
        }

        public UnitRow Unit(string propertyName, string unitNumber)
        {
            var cells = browser.Find(By.XPath(UnitRowXPath(propertyName, unitNumber))).FindElements(By.TagName("td"))
                .Select(td => td.Text.Trim()).ToList();
            return new UnitRow(cells[0], cells[1], cells[2], cells[3], cells[4]);
        }
    }
}
