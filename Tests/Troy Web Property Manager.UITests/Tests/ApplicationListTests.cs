using System.Text.Json;
using Troy_Web_Property_Manager.UITests.Infrastructure;
using Troy_Web_Property_Manager.UITests.Pages;
using Troy_Web_Property_Manager.UITests.Workflows;

namespace Troy_Web_Property_Manager.UITests.Tests
{
    /// <summary>
    /// Functional 6.a: the application list, filtered by status and property. Applicants see their own applications;
    /// property managers see all of them (every application that has been submitted).
    /// </summary>
    [Collection(UiCollection.Name)]
    public sealed class ApplicationListTests(UiFixture app)
    {
        [Fact]
        public async Task Applicant_SeesOnlyTheirOwnApplications()
        {
            var listing = await app.CreateListingAsync(new UnitData("101"), new UnitData("102"));
            var me = await app.CreateApplicantAsync("me");
            using var mine = app.NewBrowserAs(me);
            var draft = mine.ApplyFor(listing, "101").Id;
            var submitted = mine.CreateSubmittedApplication(listing, "102");
            using var someoneElse = app.NewBrowserAs(await app.CreateApplicantAsync("else"));
            var theirs = someoneElse.CreateSubmittedApplication(listing, "101");

            var list = new ApplicationListPage(mine).Open();

            Assert.Equal("My applications", mine.Text(OpenQA.Selenium.By.TagName("h1")));
            Assert.Equal([submitted, draft], list.Ids);
            Assert.DoesNotContain(theirs, list.Ids);
            var row = list.Rows.Single(r => r.Id == submitted);
            Assert.Equal($"{listing.Name} · 102", row.PropertyUnit);
            Assert.Equal("alex.tester@example.com", row.Applicant);
            Assert.Equal("Submitted", row.Status);
            Assert.NotEqual("", row.Submitted);
            Assert.Equal("", list.Rows.Single(r => r.Id == draft).Submitted);
        }

        [Fact]
        public async Task Manager_SeesEveryonesSubmittedApplications_AndFiltersByPropertyAndStatus()
        {
            var listing = await app.CreateListingAsync(new UnitData("101"), new UnitData("102"));
            using var first = app.NewBrowserAs(await app.CreateApplicantAsync("first"));
            using var second = app.NewBrowserAs(await app.CreateApplicantAsync("second"));
            var waiting = first.CreateSubmittedApplication(listing, "101");
            var claimed = second.CreateSubmittedApplication(listing, "102");
            var draft = second.ApplyFor(listing, "101").Id;
            using var manager = app.NewBrowserAs(await app.CreateManagerAsync());
            manager.Claim(claimed);
            var list = new ApplicationListPage(manager).Open();
            Assert.Equal("Applications", manager.Text(OpenQA.Selenium.By.TagName("h1")));

            list.Filter(property: listing.Name);
            Assert.Equal([claimed, waiting], list.Ids);
            Assert.DoesNotContain(draft, list.Ids);

            list.Filter(status: "Under Review");
            Assert.Equal([claimed], list.Ids);

            list.Filter(status: "Submitted");
            Assert.Equal([waiting], list.Ids);

            list.Filter(status: "Approved");
            Assert.Empty(list.Ids);
            Assert.Equal("No applications found.", list.Status);
        }

        [Fact]
        public async Task Filters_AreKeptInTheUrl_SoARefreshShowsTheSameList()
        {
            var listing = await app.CreateListingAsync();
            using var applicant = app.NewBrowserAs(await app.CreateApplicantAsync());
            var id = applicant.CreateSubmittedApplication(listing);
            using var manager = app.NewBrowserAs(await app.CreateManagerAsync());
            var list = new ApplicationListPage(manager).Open();
            list.Filter(status: "Submitted", property: listing.Name);

            manager.Reload();
            list.WaitForGrid();

            Assert.Equal([id], list.Ids);
            Assert.Equal("Submitted", new OpenQA.Selenium.Support.UI.SelectElement(manager.Find(OpenQA.Selenium.By.Id("Status"))).SelectedOption.Text);
        }

        [Fact]
        public async Task OpeningARowGoesToThatApplication()
        {
            var listing = await app.CreateListingAsync();
            using var applicant = app.NewBrowserAs(await app.CreateApplicantAsync());
            var id = applicant.CreateSubmittedApplication(listing);

            var page = new ApplicationListPage(applicant).Open().OpenApplication(id);

            Assert.Equal(id, page.Id);
        }
    }

    /// <summary>
    /// Bonus 1: paging and sorting done in the database, the list as a reusable grid view component driven by a JSON
    /// endpoint that returns the page of rows and the filtered total, documented with OpenAPI. Uses the seeded demo
    /// data, which has more than one page of applications.
    /// </summary>
    [Collection(UiCollection.Name)]
    public sealed class ApplicationGridTests(UiFixture app)
    {
        private static int TotalFrom(string summary)
        {
            return int.Parse(summary[(summary.LastIndexOf(' ') + 1)..]);
        }

        [Fact]
        public async Task Pages_TenAtATime_AndThePageSizeCanChange()
        {
            using var manager = app.NewBrowserAs(await app.CreateManagerAsync());
            var list = new ApplicationListPage(manager).Open();
            var total = TotalFrom(list.Summary);
            Assert.True(total > 10, $"The demo data should give more than one page, got {total}.");
            Assert.Equal($"1–10 of {total}", list.Summary);
            var firstPage = list.Ids;
            Assert.Equal(10, firstPage.Count);

            list.GoToPage(2);
            Assert.Equal($"11–{Math.Min(20, total)} of {total}", list.Summary);
            Assert.Empty(list.Ids.Intersect(firstPage));
            Assert.Contains("page=2", manager.PathAndQuery);

            manager.Reload();
            list.WaitForGrid();
            Assert.StartsWith("11–", list.Summary);

            list.SetPageSize(25);
            Assert.Equal($"1–{Math.Min(25, total)} of {total}", list.Summary);
        }

        [Fact]
        public async Task ColumnHeaders_Sort_AndASecondClickReverses()
        {
            using var manager = app.NewBrowserAs(await app.CreateManagerAsync());
            var list = new ApplicationListPage(manager).Open();
            Assert.Equal("descending", list.SortState("#"));
            var newestFirst = list.Ids;
            Assert.Equal(newestFirst.OrderByDescending(i => i), newestFirst);

            list.SortBy("#");
            Assert.Equal("ascending", list.SortState("#"));
            Assert.Equal(list.Ids.OrderBy(i => i), list.Ids);

            list.SortBy("Status");
            Assert.Equal("ascending", list.SortState("Status"));
            var statuses = list.Rows.Select(r => r.Status.Replace(" ", "")).ToList();
            Assert.Equal(statuses.Order(StringComparer.OrdinalIgnoreCase), statuses);

            list.SortBy("Status");
            Assert.Equal("descending", list.SortState("Status"));
            statuses = list.Rows.Select(r => r.Status.Replace(" ", "")).ToList();
            Assert.Equal(statuses.OrderDescending(StringComparer.OrdinalIgnoreCase), statuses);
        }

        [Fact]
        public async Task JsonEndpoint_ReturnsOnePageAndTheFilteredTotal()
        {
            using var manager = app.NewBrowserAs(await app.CreateManagerAsync());

            var page = JsonDocument.Parse(manager.Fetch("/api/applications?pageSize=3&page=2&sort=Id&dir=Asc").Body).RootElement;
            var all = JsonDocument.Parse(manager.Fetch("/api/applications?pageSize=100").Body).RootElement;
            var approved = JsonDocument.Parse(manager.Fetch("/api/applications?status=Approved&pageSize=100").Body).RootElement;

            Assert.Equal(3, page.GetProperty("items").GetArrayLength());
            Assert.Equal(2, page.GetProperty("page").GetInt32());
            Assert.Equal(all.GetProperty("total").GetInt32(), page.GetProperty("total").GetInt32());
            var approvedItems = approved.GetProperty("items").EnumerateArray().ToList();
            Assert.Equal(approved.GetProperty("total").GetInt32(), approvedItems.Count);
            Assert.All(approvedItems, i => Assert.Equal("Approved", i.GetProperty("status").GetString()));
            Assert.True(approvedItems.Count < all.GetProperty("total").GetInt32());
        }

        [Fact]
        public async Task JsonEndpoint_RejectsBadQueryValues()
        {
            using var manager = app.NewBrowserAs(await app.CreateManagerAsync());

            var result = manager.Fetch("/api/applications?sort=Nonsense&pageSize=500");

            Assert.Equal(400, result.Status);
            Assert.Contains("\"Sort\"", result.Body);
            Assert.Contains("\"PageSize\"", result.Body);
        }

        [Fact]
        public async Task OpenApiDocument_DescribesTheEndpoint()
        {
            using var browser = app.NewBrowser();

            var result = browser.Fetch("/openapi/v1.json");

            Assert.Equal(200, result.Status);
            var doc = JsonDocument.Parse(result.Body).RootElement;
            var get = doc.GetProperty("paths").GetProperty("/api/applications").GetProperty("get");
            Assert.Equal("ListApplications", get.GetProperty("operationId").GetString());
            var parameters = get.GetProperty("parameters").EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToList();
            Assert.Equal(["Status", "PropertyId", "Sort", "Dir", "Page", "PageSize"], parameters);
            foreach (var code in new[] { "200", "400", "401", "403" }) Assert.True(get.GetProperty("responses").TryGetProperty(code, out _), code);
        }
    }
}
