using OpenQA.Selenium;
using Troy_Web_Property_Manager.UITests.Infrastructure;

namespace Troy_Web_Property_Manager.UITests.Pages
{
    /// <summary>One row on the Available units page.</summary>
    public sealed record AvailableUnitRow(string Property, string Unit, int Bedrooms, string Rent, string Type);

    /// <summary>The applicants' Available units page (2.b, 2.d): filters, sortable headers, and Apply.</summary>
    public sealed class UnitsPage(Browser browser)
    {
        public const string Path = "/Units";

        public UnitsPage Open(string query = "")
        {
            browser.Go(Path + query);
            return this;
        }

        public IReadOnlyList<AvailableUnitRow> Rows =>
            browser.FindAll(By.CssSelector("main table tbody tr")).Select(tr =>
            {
                var c = tr.FindElements(By.TagName("td")).Select(td => td.Text.Trim()).ToList();
                return new AvailableUnitRow(c[0], c[1], int.Parse(c[2]), c[3], c[4]);
            }).ToList();

        /// <summary>The rows for one property (tests use their own properties, so this ignores the demo data).</summary>
        public IReadOnlyList<AvailableUnitRow> RowsFor(string propertyName)
        {
            return Rows.Where(r => r.Property == propertyName).ToList();
        }

        public bool Lists(string propertyName, string unit)
        {
            return RowsFor(propertyName).Any(r => r.Unit == unit);
        }

        public void Filter(string? property = null, string? bedrooms = null)
        {
            if (property is not null) browser.SelectByText(By.Id("propertyId"), property);
            if (bedrooms is not null) browser.SelectByText(By.Id("bedrooms"), bedrooms);
            browser.ClickAndWaitForPage(By.XPath(Xp.Button("Filter")));
        }

        /// <summary>Clicks a sortable column header (Property, Bedrooms, Rent or Type).</summary>
        public void SortBy(string header)
        {
            browser.ClickAndWaitForPage(By.XPath($"//thead//a[normalize-space()={Xp.Literal(header)}]"));
        }

        /// <summary>The aria-sort value on a header ("ascending", "descending" or "none").</summary>
        public string SortState(string header)
        {
            return browser.Find(By.XPath($"//thead//th[.//a[normalize-space()={Xp.Literal(header)}]]")).GetAttribute("aria-sort") ?? "";
        }

        /// <summary>Clicks Apply on a unit and returns the application it opens (a new draft, or the one already open).</summary>
        public ApplicationPage Apply(string propertyName, string unit)
        {
            browser.ClickAndWaitForPage(By.XPath(
                $"//tbody/tr[td[1][normalize-space()={Xp.Literal(propertyName)}] and td[2][normalize-space()={Xp.Literal(unit)}]]{Xp.Button("Apply")}"));
            return new ApplicationPage(browser);
        }
    }

    /// <summary>One row of the application list grid.</summary>
    public sealed record ApplicationRow(int Id, string PropertyUnit, string Applicant, string Status, string Submitted);

    /// <summary>
    /// The application list (6.a, bonus 1): the filter form above the data grid, which loads its rows from
    /// /api/applications. Every read waits for the grid to finish loading.
    /// </summary>
    public sealed class ApplicationListPage(Browser browser)
    {
        public const string Path = "/Applications";

        public ApplicationListPage Open(string query = "")
        {
            browser.Go(Path + query);
            WaitForGrid();
            return this;
        }

        /// <summary>Waits until the grid has drawn a result: rows, "No applications found.", or an error.</summary>
        public void WaitForGrid()
        {
            browser.WaitUntil(() =>
                    browser.Js("""
                        const g = document.getElementById('application-grid');
                        if (!g || g.querySelector('table').hasAttribute('aria-busy')) return false;
                        const status = g.querySelector('.data-grid-status');
                        return !(status && !status.hidden && status.textContent.trim() === 'Loading…');
                        """) is true,
                "the application grid to load");
        }

        public IReadOnlyList<ApplicationRow> Rows
        {
            get
            {
                WaitForGrid();
                return browser.FindAll(By.CssSelector("#application-grid tbody tr")).Select(tr =>
                {
                    var c = tr.FindElements(By.TagName("td")).Select(td => td.Text.Trim()).ToList();
                    return new ApplicationRow(int.Parse(c[0]), c[1], c[2], c[3], c[4]);
                }).ToList();
            }
        }

        public IReadOnlyList<int> Ids => Rows.Select(r => r.Id).ToList();

        /// <summary>"1–10 of 32" under the grid, or "" when it's hidden.</summary>
        public string Summary
        {
            get
            {
                WaitForGrid();
                return browser.IsVisible(By.CssSelector("#application-grid .data-grid-footer"))
                    ? browser.Text(By.CssSelector("#application-grid .data-grid-summary"))
                    : "";
            }
        }

        /// <summary>The grid's status line ("No applications found.", or an error), or "".</summary>
        public string Status
        {
            get
            {
                WaitForGrid();
                return browser.FindAll(By.CssSelector("#application-grid .data-grid-status")).Select(e => e.Text.Trim()).FirstOrDefault() ?? "";
            }
        }

        public void Filter(string? status = null, string? property = null)
        {
            if (status is not null) browser.SelectByText(By.Id("Status"), status);
            if (property is not null) browser.SelectByText(By.Id("PropertyId"), property);
            // The grid reloads in place (no page load). grid.js marks the table busy as soon as the form is submitted,
            // so waiting for the grid waits for the new result.
            browser.Click(By.XPath(Xp.Button("Filter")));
            WaitForGrid();
        }

        public void SortBy(string header)
        {
            var before = browser.PathAndQuery;
            browser.Click(By.XPath($"//div[@id='application-grid']//thead//button[normalize-space()={Xp.Literal(header)}]"));
            browser.WaitUntil(() => browser.PathAndQuery != before, $"the grid to sort by {header}");
            WaitForGrid();
        }

        public string SortState(string header)
        {
            return browser.Find(By.XPath($"//div[@id='application-grid']//thead//th[.//button[normalize-space()={Xp.Literal(header)}]]"))
                .GetAttribute("aria-sort") ?? "";
        }

        public void GoToPage(int page)
        {
            var before = browser.PathAndQuery;
            browser.Click(By.CssSelector($"#application-grid .pagination button[aria-label='Page {page}']"));
            browser.WaitUntil(() => browser.PathAndQuery != before, $"page {page}");
            WaitForGrid();
        }

        public void SetPageSize(int size)
        {
            browser.SelectByValue(By.CssSelector("#application-grid .data-grid-page-size"), size.ToString());
            WaitForGrid();
        }

        public ApplicationPage OpenApplication(int id)
        {
            browser.ClickAndWaitForPage(By.XPath($"//div[@id='application-grid']//tbody//a[normalize-space()='{id}']"));
            return new ApplicationPage(browser);
        }
    }

    /// <summary>One row in a review queue section.</summary>
    public sealed record QueueRow(int Id, string Text);

    /// <summary>The managers' review queue (bonus 2): My claims, Waiting for review, Claimed by other managers.</summary>
    public sealed class QueuePage(Browser browser)
    {
        public const string Path = "/Applications/Queue";
        public const string Mine = "My claims";
        public const string Waiting = "Waiting for review";
        public const string ClaimedByOthers = "Claimed by other managers";

        public QueuePage Open()
        {
            browser.Go(Path);
            return this;
        }

        private static string RowsXPath(string section)
        {
            // Rows whose nearest section heading above them is this one.
            return $"//tbody/tr[preceding::h2[1][normalize-space()={Xp.Literal(section)}]]";
        }

        public IReadOnlyList<QueueRow> Rows(string section)
        {
            return browser.FindAll(By.XPath(RowsXPath(section)))
                .Select(tr => new QueueRow(int.Parse(tr.FindElement(By.CssSelector("td a")).Text), tr.Text)).ToList();
        }

        public IReadOnlyList<int> Ids(string section)
        {
            return Rows(section).Select(r => r.Id).ToList();
        }

        /// <summary>Claims from the Waiting section; lands on the application page.</summary>
        public ApplicationPage Claim(int id)
        {
            browser.ClickAndWaitForPage(By.XPath($"{RowsXPath(Waiting)}[td[1][normalize-space()='{id}']]{Xp.Button("Claim")}"));
            return new ApplicationPage(browser);
        }

        /// <summary>Releases from whichever section the row is in, confirming in the modal. The page reloads.</summary>
        public void Release(int id)
        {
            browser.Click(By.XPath($"//tbody/tr[td[1][normalize-space()='{id}']]{Xp.Button("Release")}"));
            browser.WaitForModal("Release application");
            browser.ClickModalButtonAndWaitForPage("Release");
        }
    }
}
