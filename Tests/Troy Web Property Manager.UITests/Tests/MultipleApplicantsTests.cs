using Troy_Web_Property_Manager.Services;
using Troy_Web_Property_Manager.UITests.Infrastructure;
using Troy_Web_Property_Manager.UITests.Pages;
using Troy_Web_Property_Manager.UITests.Workflows;

namespace Troy_Web_Property_Manager.UITests.Tests
{
    /// <summary>
    /// Bonus 5: more than one applicant on an application. Any of them can view and edit it and the ownership checks
    /// cover all of them. With two editing at once, saves to different sections don't interfere, and a second save to
    /// the same section is rejected as stale with a message to reload.
    /// </summary>
    [Collection(UiCollection.Name)]
    public sealed class MultipleApplicantsTests(UiFixture app)
    {
        private async Task<(Browser Starter, Browser CoApplicant, TestUser CoUser, int Id)> SharedDraftAsync()
        {
            var listing = await app.CreateListingAsync();
            var starter = app.NewBrowserAs(await app.CreateApplicantAsync("starter"));
            var coUser = await app.CreateApplicantAsync("co");
            var page = starter.ApplyFor(listing);
            page.AddApplicant(coUser.Email);
            return (starter, app.NewBrowserAs(coUser), coUser, page.Id);
        }

        [Fact]
        public async Task AddedApplicant_CanSeeAndEditTheApplication()
        {
            var (starter, co, coUser, id) = await SharedDraftAsync();
            using var s = starter;
            using var c = co;

            Assert.Contains(id, new ApplicationListPage(co).Open().Ids);
            var page = new ApplicationPage(co).Open(id);
            Assert.Contains(page.Applicants, a => a.StartsWith(coUser.Email) && a.Contains("(you)"));
            Assert.Contains(page.Applicants, a => a.Contains("(started it)"));

            page.FillApplicantInformation(ApplicantInfo.Valid() with { Name = "Edited By Co" });
            page.Continue();

            Assert.Equal("Edited By Co", new ApplicationPage(starter).Open(id, "ApplicantInformation").ReadApplicantInformation().Name);
        }

        [Theory]
        [InlineData("nobody-here@uitest.local", "There's no applicant account with that email.")]
        [InlineData("manager", "There's no applicant account with that email.")]
        [InlineData("self", "They're already on this application.")]
        [InlineData("not-an-email", "The Email field is not a valid e-mail address.")]
        public async Task AddApplicant_WithABadEmail_ShowsTheErrorInTheModal(string email, string expected)
        {
            var listing = await app.CreateListingAsync();
            var starterUser = await app.CreateApplicantAsync("starter");
            using var starter = app.NewBrowserAs(starterUser);
            var page = starter.ApplyFor(listing);
            email = email switch
            {
                "manager" => (await app.CreateManagerAsync()).Email,
                "self" => starterUser.Email,
                _ => email
            };

            page.OpenAddApplicant();
            page.FillAddApplicantModal(email);

            starter.WaitForModalText(expected);
            Assert.Single(page.Open(page.Id).Applicants);
        }

        [Fact]
        public async Task RemovedApplicant_LosesAccess_AndAnApplicantCanLeave()
        {
            var (starter, co, coUser, id) = await SharedDraftAsync();
            using var s = starter;
            using var c = co;
            var starterPage = new ApplicationPage(starter).Open(id);
            // The starter can't be removed, so there's no Remove on their own badge.
            Assert.False(starter.IsVisible(OpenQA.Selenium.By.XPath($"//span[contains(@class,'badge')][contains(., '(started it)')]{Xp.Button("Leave")}")));

            starterPage.RemoveApplicant(coUser.Email);
            Assert.Equal(404, co.Fetch(ApplicationPage.PathFor(id)).Status);

            starterPage.AddApplicant(coUser.Email);
            new ApplicationPage(co).Open(id).RemoveApplicant(coUser.Email, isYou: true);
            Assert.Equal("You left the application.", new Flash(co).Success?.Trim());
            Assert.StartsWith(ApplicationListPage.Path, co.PathAndQuery);
            Assert.DoesNotContain(id, new ApplicationListPage(co).Open().Ids);
        }

        [Fact]
        public async Task TwoApplicantsSavingTheSameSection_TheSecondIsRejectedAsStale()
        {
            var (starter, co, _, id) = await SharedDraftAsync();
            using var s = starter;
            using var c = co;
            var starterPage = new ApplicationPage(starter).Open(id, "ApplicantInformation");
            var coPage = new ApplicationPage(co).Open(id, "ApplicantInformation");

            starterPage.FillApplicantInformation(ApplicantInfo.Valid() with { Name = "Starter Saved First" });
            starterPage.Continue();
            coPage.FillApplicantInformation(ApplicantInfo.Valid() with { Name = "Co Saved Second" });
            coPage.Continue();

            Assert.Contains(ApplicationService.SectionChangedMessage, coPage.FormErrors);
            Assert.Equal("Applicant information", coPage.Section);
            Assert.Equal("Starter Saved First", coPage.Open(id, "ApplicantInformation").ReadApplicantInformation().Name);
        }

        [Fact]
        public async Task TwoApplicantsSavingDifferentSections_DontInterfere()
        {
            var (starter, co, _, id) = await SharedDraftAsync();
            using var s = starter;
            using var c = co;
            var starterPage = new ApplicationPage(starter).Open(id, "ApplicantInformation");
            var coPage = new ApplicationPage(co).Open(id, "ResidenceHistory");

            starterPage.FillApplicantInformation(ApplicantInfo.Valid());
            starterPage.Continue();
            coPage.AddResidence(ResidenceData.Valid());
            coPage.Continue();

            Assert.Equal("Residence history", starterPage.Section);
            Assert.Equal("Summary", coPage.Section);
            Assert.Empty(coPage.Blockers);
        }

        [Fact]
        public async Task ResidenceModal_OpenedBeforeAnotherApplicantSaved_IsRejectedAsStale()
        {
            var (starter, co, _, id) = await SharedDraftAsync();
            using var s = starter;
            using var c = co;
            var coPage = new ApplicationPage(co).Open(id, "ResidenceHistory");
            coPage.OpenAddResidence();

            new ApplicationPage(starter).Open(id, "ResidenceHistory").AddResidence(ResidenceData.Valid("1 Starter St"));
            coPage.FillResidenceModal(ResidenceData.Valid("2 Co St"));

            co.WaitForModalText(ApplicationService.SectionChangedMessage);
            Assert.Equal(["1 Starter St"], coPage.Open(id, "ResidenceHistory").Residences.Select(r => r.Address));
        }

        [Fact]
        public async Task SubmittingASummaryThatChangedSinceItWasLoaded_IsRejected()
        {
            var (starter, co, _, id) = await SharedDraftAsync();
            using var s = starter;
            using var c = co;
            var starterPage = new ApplicationPage(starter).Open(id).CompleteSections();
            var coPage = new ApplicationPage(co).Open(id, "ApplicantInformation");

            coPage.FillApplicantInformation(ApplicantInfo.Valid() with { Phone = "518-555-0177" });
            coPage.Continue();
            starterPage.Submit();

            Assert.Contains(ApplicationService.ChangedBeforeSubmitMessage, starterPage.FormErrors);
            Assert.Equal("Draft", starterPage.Status);
        }

        [Fact]
        public async Task OnceSubmitted_NoOneCanBeAddedOrRemoved()
        {
            var (starter, co, _, id) = await SharedDraftAsync();
            using var s = starter;
            using var c = co;
            var page = new ApplicationPage(starter).Open(id).CompleteSections();
            page.Submit();

            Assert.False(page.HasButton("Add applicant"));
            Assert.False(page.HasButton("Remove"));
            Assert.Equal(404, starter.Fetch($"/Applications/AddApplicant/{id}", ajax: true).Status);
        }
    }
}
