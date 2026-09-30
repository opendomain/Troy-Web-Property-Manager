using System.Text.Json;
using Troy_Web_Property_Manager.UITests.Infrastructure;
using Troy_Web_Property_Manager.UITests.Pages;
using Troy_Web_Property_Manager.UITests.Workflows;

namespace Troy_Web_Property_Manager.UITests.Tests
{
    /// <summary>
    /// The Challenge's "ensure the permissions in the controllers and the UI reflect this": each role only reaches its
    /// own pages (a 403 otherwise), nobody reaches someone else's application (a 404, so it isn't even admitted to
    /// exist), anonymous users are sent to log in, and posts need the antiforgery token. Also 4.d's "Controllers reject
    /// posts that are not allowed", tested by tampering with the page the way a hand-made request would.
    /// </summary>
    [Collection(UiCollection.Name)]
    public sealed class SecurityTests(UiFixture app)
    {
        [Theory]
        [InlineData("/Properties")]
        [InlineData("/Units")]
        [InlineData("/Applications")]
        [InlineData("/Applications/Queue")]
        [InlineData("/Applications/Edit/1")]
        [InlineData("/Identity/Account/Manage")]
        public void AnonymousVisitor_IsSentToLogIn(string path)
        {
            using var browser = app.NewBrowser();

            browser.Go(path);

            Assert.StartsWith(LoginPage.Path + "?ReturnUrl=", browser.PathAndQuery);
        }

        [Theory]
        [InlineData("/Properties")]
        [InlineData("/Properties/Edit")]
        [InlineData("/Applications/Queue")]
        public async Task Applicant_OnAManagerPage_GetsAccessDenied(string path)
        {
            using var browser = app.NewBrowserAs(await app.CreateApplicantAsync());

            browser.Go(path);
            Assert.StartsWith("/Identity/Account/AccessDenied", browser.PathAndQuery);
            Assert.Equal(403, browser.Fetch(path, ajax: true).Status);
        }

        [Fact]
        public async Task Manager_OnAnApplicantPage_GetsAccessDenied()
        {
            using var browser = app.NewBrowserAs(await app.CreateManagerAsync());

            browser.Go(UnitsPage.Path);

            Assert.StartsWith("/Identity/Account/AccessDenied", browser.PathAndQuery);
            Assert.Equal(403, browser.Fetch(UnitsPage.Path, ajax: true).Status);
        }

        [Fact]
        public async Task Manager_CannotApplyForAUnit_EvenByPostingDirectly()
        {
            var listing = await app.CreateListingAsync();
            var unitId = await app.WithDbAsync(db => Task.FromResult(db.Units.Single(u => u.PropertyId == listing.PropertyId).Id));
            using var browser = app.NewBrowserAs(await app.CreateManagerAsync());
            browser.Go("/Applications");

            var result = browser.Fetch("/Applications/Start", "POST", $"unitId={unitId}&__RequestVerificationToken={Uri.EscapeDataString(browser.AntiforgeryToken())}", ajax: true);

            Assert.Equal(403, result.Status);
        }

        [Fact]
        public async Task Applicant_CannotSeeOrChangeAnotherApplicantsApplication()
        {
            var listing = await app.CreateListingAsync();
            using var owner = app.NewBrowserAs(await app.CreateApplicantAsync("owner"));
            var id = owner.ApplyFor(listing).Id;
            using var stranger = app.NewBrowserAs(await app.CreateApplicantAsync("stranger"));
            stranger.Go(ApplicationListPage.Path);
            var token = Uri.EscapeDataString(stranger.AntiforgeryToken());

            // Not found, rather than forbidden: it doesn't even admit the application exists.
            Assert.Equal(404, stranger.Fetch(ApplicationPage.PathFor(id)).Status);
            Assert.Equal(404, stranger.Fetch($"/Applications/Residence/{id}", ajax: true).Status);
            Assert.Equal(404, stranger.Fetch($"/Applications/Withdraw/{id}", "POST", $"__RequestVerificationToken={token}", ajax: true).Status);
            Assert.DoesNotContain(id, new ApplicationListPage(stranger).Open().Ids);

            // And it's still a draft for its owner.
            Assert.Equal("Draft", new ApplicationPage(owner).Open(id).Status);
        }

        [Fact]
        public async Task Manager_CannotSeeADraftThatWasNeverSubmitted()
        {
            var listing = await app.CreateListingAsync();
            using var applicant = app.NewBrowserAs(await app.CreateApplicantAsync());
            var id = applicant.ApplyFor(listing).Id;
            using var manager = app.NewBrowserAs(await app.CreateManagerAsync());

            Assert.Equal(404, manager.Fetch(ApplicationPage.PathFor(id)).Status);
            Assert.DoesNotContain(id, new ApplicationListPage(manager).Open($"?propertyId={listing.PropertyId}").Ids);
        }

        [Fact]
        public async Task Posts_WithoutTheAntiforgeryToken_AreRejected()
        {
            var listing = await app.CreateListingAsync();
            using var browser = app.NewBrowserAs(await app.CreateApplicantAsync());
            var page = browser.ApplyFor(listing);
            var id = page.Id;

            var withoutToken = browser.Fetch($"/Applications/Withdraw/{id}", "POST", "", ajax: true);

            Assert.Equal(400, withoutToken.Status);
            Assert.Equal("Draft", page.Open(id).Status);
        }

        [Fact]
        public async Task SubmittedApplication_TamperedIntoAnEdit_IsRejectedByTheServer()
        {
            var listing = await app.CreateListingAsync();
            using var browser = app.NewBrowserAs(await app.CreateApplicantAsync());
            var id = browser.CreateSubmittedApplication(listing);
            var page = new ApplicationPage(browser).Open(id, "ApplicantInformation");
            Assert.True(page.IsApplicantInformationReadOnly);

            // Unlock the read-only form and post a Continue on section 1, like a hand-made request would.
            browser.Js("""
                const form = document.querySelector('main form[action*="/Applications/Edit/"]');
                form.querySelectorAll('fieldset').forEach(f => f.disabled = false);
                document.getElementById('ApplicantInformation_Name').value = 'Tampered Name';
                const button = document.createElement('button');
                button.type = 'submit'; button.name = 'command'; button.value = 'continue'; button.setAttribute('formnovalidate', '');
                form.appendChild(button);
                """);
            browser.ClickAndWaitForPage(OpenQA.Selenium.By.CssSelector("main form button[value='continue']"));

            Assert.Contains("This application can no longer be edited.", page.FormErrors);
            Assert.Equal("Alex Tester", page.Open(id, "ApplicantInformation").ReadApplicantInformation().Name);
            Assert.Equal("Submitted", page.Status);
        }

        [Fact]
        public async Task Applicant_CannotUseTheManagersActions()
        {
            var listing = await app.CreateListingAsync();
            using var browser = app.NewBrowserAs(await app.CreateApplicantAsync());
            var id = browser.CreateSubmittedApplication(listing);
            var token = Uri.EscapeDataString(browser.AntiforgeryToken());

            Assert.Equal(403, browser.Fetch($"/Applications/Claim/{id}", "POST", $"__RequestVerificationToken={token}", ajax: true).Status);
            Assert.Equal(403, browser.Fetch($"/Applications/Review/{id}", ajax: true).Status);
            Assert.Equal(403, browser.Fetch($"/Applications/Review/{id}", "POST", $"Outcome=Approve&__RequestVerificationToken={token}", ajax: true).Status);
            Assert.Equal("Submitted", new ApplicationPage(browser).Open(id).Status);
        }

        [Fact]
        public async Task JsonApi_NeedsSignIn_AndOnlyReturnsYourOwnApplications()
        {
            var listing = await app.CreateListingAsync();
            using var anonymous = app.NewBrowser();
            Assert.Equal(401, anonymous.Fetch("/api/applications").Status);

            using var browser = app.NewBrowserAs(await app.CreateApplicantAsync());
            var id = browser.ApplyFor(listing).Id;
            var json = JsonDocument.Parse(browser.Fetch("/api/applications").Body).RootElement;

            Assert.Equal(1, json.GetProperty("total").GetInt32());
            Assert.Equal(id, json.GetProperty("items")[0].GetProperty("id").GetInt32());
        }
    }
}
