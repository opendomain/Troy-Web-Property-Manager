using Troy_Web_Property_Manager.UITests.Infrastructure;
using Troy_Web_Property_Manager.UITests.Pages;
using Troy_Web_Property_Manager.UITests.Workflows;

namespace Troy_Web_Property_Manager.UITests.Tests
{
    /// <summary>
    /// Bonus 2: a manager claims a submitted application (Under Review) before completing it, and it can be released
    /// back to the queue. Other managers see the claim but can't review it.
    /// </summary>
    [Collection(UiCollection.Name)]
    public sealed class ReviewQueueTests(UiFixture app)
    {
        [Fact]
        public async Task SubmittedApplication_WaitsInTheQueue_AndClaimingMovesItToMyClaims()
        {
            var listing = await app.CreateListingAsync();
            using var applicant = app.NewBrowserAs(await app.CreateApplicantAsync());
            var id = applicant.CreateSubmittedApplication(listing);
            using var manager = app.NewBrowserAs(await app.CreateManagerAsync());
            var queue = new QueuePage(manager).Open();
            Assert.Contains(id, queue.Ids(QueuePage.Waiting));

            // Nothing to review until it's claimed.
            var unclaimed = new ApplicationPage(manager).Open(id);
            Assert.False(unclaimed.HasButton("Review"));
            Assert.Equal(404, manager.Fetch($"/Applications/Review/{id}", ajax: true).Status);

            var page = queue.Open().Claim(id);

            Assert.Equal("You claimed this application. It's now under review.", new Flash(manager).Success?.Trim());
            Assert.Equal("Under Review", page.Status);
            Assert.StartsWith("Claimed by you", page.ClaimLine);
            Assert.True(page.HasButton("Review"));
            queue.Open();
            Assert.Contains(id, queue.Ids(QueuePage.Mine));
            Assert.DoesNotContain(id, queue.Ids(QueuePage.Waiting));
        }

        [Fact]
        public async Task AnotherManager_SeesTheClaim_CannotReviewIt_ButCanReleaseIt()
        {
            var listing = await app.CreateListingAsync();
            using var applicant = app.NewBrowserAs(await app.CreateApplicantAsync());
            var id = applicant.CreateSubmittedApplication(listing);
            var claimer = await app.CreateManagerAsync("claimer");
            using var first = app.NewBrowserAs(claimer);
            first.Claim(id);
            using var second = app.NewBrowserAs(await app.CreateManagerAsync("other"));

            var page = new ApplicationPage(second).Open(id);
            Assert.StartsWith($"Claimed by {claimer.Email}", page.ClaimLine);
            Assert.False(page.HasButton("Review"));
            Assert.Equal(404, second.Fetch($"/Applications/Review/{id}", ajax: true).Status);
            var queue = new QueuePage(second).Open();
            Assert.Contains(id, queue.Ids(QueuePage.ClaimedByOthers));
            Assert.Contains(claimer.Email, queue.Rows(QueuePage.ClaimedByOthers).Single(r => r.Id == id).Text);

            queue.Release(id);

            Assert.Equal("The application was released back to the review queue.", new Flash(second).Success?.Trim());
            Assert.Contains(id, queue.Ids(QueuePage.Waiting));
            Assert.Equal("Submitted", new ApplicationPage(first).Open(id).Status);
        }

        [Fact]
        public async Task TwoManagersClaimingTheSameApplication_OnlyTheFirstGetsIt()
        {
            var listing = await app.CreateListingAsync();
            using var applicant = app.NewBrowserAs(await app.CreateApplicantAsync());
            var id = applicant.CreateSubmittedApplication(listing);
            using var first = app.NewBrowserAs(await app.CreateManagerAsync("first"));
            using var second = app.NewBrowserAs(await app.CreateManagerAsync("second"));
            var firstQueue = new QueuePage(first).Open();
            var secondQueue = new QueuePage(second).Open();

            firstQueue.Claim(id);
            secondQueue.Claim(id);

            Assert.Equal("This application has already been claimed.", new Flash(second).Error?.Trim());
            Assert.False(new ApplicationPage(second).HasButton("Review"));
        }

        [Fact]
        public async Task Applicant_SeesUnderReview_ButNotWhoClaimedIt()
        {
            var listing = await app.CreateListingAsync();
            using var applicant = app.NewBrowserAs(await app.CreateApplicantAsync());
            var id = applicant.CreateSubmittedApplication(listing);
            var managerUser = await app.CreateManagerAsync();
            using (var manager = app.NewBrowserAs(managerUser)) manager.Claim(id);

            var page = new ApplicationPage(applicant).Open(id);

            Assert.Equal("Under Review", page.Status);
            Assert.Null(page.ClaimLine);
            Assert.DoesNotContain(managerUser.Email, applicant.PageSource);
            Assert.True(page.HasButton("Withdraw"), "An applicant can still withdraw while it's under review.");
        }
    }

    /// <summary>
    /// Bonus 3: property manager notes, visible and editable only by managers, and never rendered or returned to an
    /// applicant - not on the page, not from the notes URLs, not from the JSON API.
    /// </summary>
    [Collection(UiCollection.Name)]
    public sealed class ManagerNotesTests(UiFixture app)
    {
        private const string Secret = "PRIVATE: second income unverified";

        private async Task<(Browser Applicant, int Id)> SubmittedAsync()
        {
            var listing = await app.CreateListingAsync();
            var applicant = app.NewBrowserAs(await app.CreateApplicantAsync());
            return (applicant, applicant.CreateSubmittedApplication(listing));
        }

        [Fact]
        public async Task Manager_AddsNotesInTheModal_AndOtherManagersSeeThem()
        {
            var (applicant, id) = await SubmittedAsync();
            using var _ = applicant;
            var author = await app.CreateManagerAsync("author");
            using var manager = app.NewBrowserAs(author);
            var page = new ApplicationPage(manager).Open(id);
            Assert.Contains("No notes yet.", page.ManagerNotes);
            manager.MarkPage();

            page.EditManagerNotes(Secret);

            Assert.True(manager.IsSamePage(), "Saving notes reloaded the page instead of redrawing the notes panel.");
            Assert.Contains($"Last updated by {author.Email}", page.ManagerNotes);
            using var other = app.NewBrowserAs(await app.CreateManagerAsync("reader"));
            Assert.Contains(Secret, new ApplicationPage(other).Open(id).ManagerNotes);
        }

        [Fact]
        public async Task Applicant_NeverGetsTheNotes()
        {
            var (applicant, id) = await SubmittedAsync();
            using var _ = applicant;
            using (var manager = app.NewBrowserAs(await app.CreateManagerAsync()))
            {
                new ApplicationPage(manager).Open(id).EditManagerNotes(Secret);
                // Return it, so the applicant gets the editable page too.
                manager.ReviewApplication(id, "Return", "Please update your phone.");
            }

            foreach (var section in new[] { "ApplicantInformation", "ResidenceHistory", "Summary" })
            {
                var page = new ApplicationPage(applicant).Open(id, section);
                Assert.False(page.HasManagerNotesPanel);
                Assert.DoesNotContain(Secret, applicant.PageSource);
            }
            Assert.Equal(403, applicant.Fetch($"/Applications/ManagerNotes/{id}", ajax: true).Status);
            Assert.Equal(403, applicant.Fetch($"/Applications/EditManagerNotes/{id}", ajax: true).Status);
            Assert.DoesNotContain(Secret, applicant.Fetch("/api/applications").Body);
            Assert.DoesNotContain(Secret, applicant.Fetch($"/Applications/Residences/{id}", ajax: true).Body);
        }

        [Fact]
        public async Task TwoManagersEditingTheNotes_TheSecondSaveIsRejectedAsStale()
        {
            var (applicant, id) = await SubmittedAsync();
            using var _ = applicant;
            using var first = app.NewBrowserAs(await app.CreateManagerAsync("first"));
            using var second = app.NewBrowserAs(await app.CreateManagerAsync("second"));
            var firstPage = new ApplicationPage(first).Open(id);
            var secondPage = new ApplicationPage(second).Open(id);
            secondPage.OpenEditManagerNotes();

            firstPage.EditManagerNotes("First manager's notes");
            secondPage.SaveManagerNotesModal("Second manager's notes");

            second.WaitForModalText("These notes were changed by someone else. Reload the page and try again.");
            Assert.Contains("First manager's notes", secondPage.Open(id).ManagerNotes);
        }

        [Fact]
        public async Task NotesLongerThan2000Characters_AreRejected()
        {
            var (applicant, id) = await SubmittedAsync();
            using var _ = applicant;
            using var manager = app.NewBrowserAs(await app.CreateManagerAsync());
            var page = new ApplicationPage(manager).Open(id);

            page.OpenEditManagerNotes();
            manager.DisableModalClientValidation();
            manager.Js("document.querySelector('#app-modal #Notes').removeAttribute('maxlength');");
            page.SaveManagerNotesModal(new string('n', 2001));

            manager.WaitForModalText("The field Notes must be a string with a maximum length of 2000.");
            Assert.Contains("No notes yet.", page.Open(id).ManagerNotes);
        }
    }
}
