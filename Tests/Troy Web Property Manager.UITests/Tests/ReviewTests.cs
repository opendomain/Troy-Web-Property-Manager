using Microsoft.EntityFrameworkCore;
using Troy_Web_Property_Manager.UITests.Infrastructure;
using Troy_Web_Property_Manager.UITests.Pages;
using Troy_Web_Property_Manager.UITests.Workflows;

namespace Troy_Web_Property_Manager.UITests.Tests
{
    /// <summary>
    /// Functional 5: a manager completes a submitted application through the review modal - Approve, Return or Deny,
    /// with a comment required for Return and Deny; Approved, Denied and Withdrawn are final; managers see the history
    /// of status changes (who, when, comment). Challenge a: a returned application can be corrected and resubmitted.
    /// </summary>
    [Collection(UiCollection.Name)]
    public sealed class ReviewTests(UiFixture app)
    {
        private async Task<(Browser Applicant, Browser Manager, TestUser ManagerUser, int Id)> SubmittedAsync()
        {
            var listing = await app.CreateListingAsync();
            var applicant = app.NewBrowserAs(await app.CreateApplicantAsync());
            var id = applicant.CreateSubmittedApplication(listing);
            var managerUser = await app.CreateManagerAsync();
            return (applicant, app.NewBrowserAs(managerUser), managerUser, id);
        }

        [Fact]
        public async Task Review_WithoutAnOutcome_ShowsAnErrorInTheModal()
        {
            var (applicant, manager, _, id) = await SubmittedAsync();
            using var a = applicant;
            using var m = manager;
            var page = manager.Claim(id);

            page.OpenReview();
            page.FillReviewModal(outcome: null, comment: null);

            manager.WaitForModalText("Choose an outcome.");
            Assert.Equal("Under Review", page.Open(id).Status);
        }

        [Theory]
        [InlineData("Return")]
        [InlineData("Deny")]
        public async Task ReturnAndDeny_NeedAComment(string outcome)
        {
            var (applicant, manager, _, id) = await SubmittedAsync();
            using var a = applicant;
            using var m = manager;
            var page = manager.Claim(id);

            page.OpenReview();
            page.FillReviewModal(outcome, "   ");

            manager.WaitForModalText("A comment is required to return or deny an application.");
            Assert.Equal("Under Review", page.Open(id).Status);
        }

        [Fact]
        public async Task Return_SendsItBackWithTheComment_AndTheApplicantCanFixAndResubmit()
        {
            var (applicant, manager, _, id) = await SubmittedAsync();
            using var a = applicant;
            using var m = manager;

            manager.ReviewApplication(id, "Return", "Please add a second residence.");
            Assert.Equal("Application returned to the applicant.", new Flash(manager).Success?.Trim());
            Assert.Equal("Returned", new ApplicationPage(manager).Status);

            var page = new ApplicationPage(applicant).Open(id);
            Assert.Equal("Returned", page.Status);
            Assert.Equal("Please add a second residence.", page.ReviewComment);
            Assert.Equal("Applicant information", page.Section);
            page.Continue();
            page.AddResidence(ResidenceData.Valid("9 Second St"));
            page.Continue();
            page.Submit();
            Assert.Equal("Submitted", page.Status);
            Assert.Null(page.ReviewComment);

            Assert.Contains(id, new QueuePage(manager).Open().Ids(QueuePage.Waiting));
        }

        [Fact]
        public async Task Deny_IsFinal_AndTheApplicantSeesWhy()
        {
            var (applicant, manager, _, id) = await SubmittedAsync();
            using var a = applicant;
            using var m = manager;

            manager.ReviewApplication(id, "Deny", "Income doesn't meet the requirement.");

            var page = new ApplicationPage(applicant).Open(id);
            Assert.Equal("Denied", page.Status);
            Assert.Equal("Income doesn't meet the requirement.", page.ReviewComment);
            Assert.False(page.HasButton("Withdraw"));
            Assert.False(page.HasButton("Continue"));
            var managerPage = new ApplicationPage(manager).Open(id);
            Assert.False(managerPage.HasButton("Claim for review"));
            Assert.False(managerPage.HasButton("Review"));
            Assert.False(managerPage.HasButton("Release"));
        }

        [Fact]
        public async Task Approve_CreatesATwelveMonthLease_AndTheUnitIsNoLongerAvailable()
        {
            var listing = await app.CreateListingAsync();
            using var applicant = app.NewBrowserAs(await app.CreateApplicantAsync());
            var id = applicant.CreateSubmittedApplication(listing);
            using var manager = app.NewBrowserAs(await app.CreateManagerAsync());

            var page = manager.Approve(id);

            Assert.Equal("Application approved; a 12-month lease was created.", new Flash(manager).Success?.Trim());
            Assert.Equal("Approved", page.Status);
            Assert.Equal("Leased", new PropertiesPage(manager).Open().Unit(listing.Name, listing.FirstUnit).Status);
            Assert.False(new UnitsPage(applicant).Open().Lists(listing.Name, listing.FirstUnit));
            var applicantPage = new ApplicationPage(applicant).Open(id);
            Assert.Equal("Approved", applicantPage.Status);
            Assert.False(applicantPage.HasButton("Withdraw"));

            // The lease itself isn't on any page, so check its term in the database.
            var lease = await app.WithDbAsync(db => db.Leases.SingleAsync(l => l.RentalApplicationId == id));
            Assert.Equal(lease.StartDate.AddMonths(12), lease.EndDate);
        }

        [Fact]
        public async Task Withdraw_WhileSubmitted_IsFinal_AndLeavesTheQueue()
        {
            var (applicant, manager, _, id) = await SubmittedAsync();
            using var a = applicant;
            using var m = manager;
            Assert.Contains(id, new QueuePage(manager).Open().Ids(QueuePage.Waiting));

            new ApplicationPage(applicant).Open(id).Withdraw();

            Assert.Equal("Withdrawn", new ApplicationPage(applicant).Status);
            Assert.DoesNotContain(id, new QueuePage(manager).Open().Ids(QueuePage.Waiting));
            var managerPage = new ApplicationPage(manager).Open(id);
            Assert.Equal("Withdrawn", managerPage.Status);
            Assert.False(managerPage.HasButton("Claim for review"));
        }

        [Fact]
        public async Task History_ShowsEveryChange_WhoAndComment_ToManagersOnly()
        {
            var (applicant, manager, managerUser, id) = await SubmittedAsync();
            using var a = applicant;
            using var m = manager;
            manager.ReviewApplication(id, "Return", "Fix the phone number.");
            var page = new ApplicationPage(applicant).Open(id);
            page.Open(id, "Summary").Submit();
            manager.Approve(id);

            var history = new ApplicationPage(manager).Open(id).History;

            Assert.Equal(7, history.Count);
            Assert.StartsWith("Created", history[0]);
            Assert.StartsWith("Draft → Submitted", history[1]);
            Assert.StartsWith("Submitted → Under Review", history[2]);
            Assert.StartsWith("Under Review → Returned (Return)", history[3]);
            Assert.Contains(managerUser.Email, history[3]);
            Assert.Contains("Fix the phone number.", history[3]);
            Assert.StartsWith("Returned → Submitted", history[4]);
            Assert.StartsWith("Under Review → Approved (Approve)", history[6]);

            var applicantPage = new ApplicationPage(applicant).Open(id);
            Assert.False(applicantPage.HasHistoryPanel);
            Assert.DoesNotContain("Fix the phone number.", applicant.PageSource);
        }
    }

    /// <summary>
    /// Functional 4.e: at submit and again at approval, the action is refused when the unit already has an active lease.
    /// Other open applications for the unit are left as they are, and the approval check prevents a second lease.
    /// </summary>
    [Collection(UiCollection.Name)]
    public sealed class ActiveLeaseTests(UiFixture app)
    {
        [Fact]
        public async Task Submit_IsBlocked_OnceTheUnitHasBeenLeased()
        {
            var listing = await app.CreateListingAsync();
            using var winner = app.NewBrowserAs(await app.CreateApplicantAsync("winner"));
            using var other = app.NewBrowserAs(await app.CreateApplicantAsync("other"));
            var otherPage = other.ApplyFor(listing).CompleteSections();
            var otherId = otherPage.Id;
            var winnerId = winner.CreateSubmittedApplication(listing);
            using var manager = app.NewBrowserAs(await app.CreateManagerAsync());
            manager.Approve(winnerId);

            otherPage.Open(otherId, "Summary");
            Assert.Contains("This unit has an active lease and is no longer available.", otherPage.Blockers);
            Assert.False(otherPage.IsSubmitEnabled);

            // The server refuses it too, even when the button is forced on.
            other.Js("document.querySelector(\"button[value='submit']\").disabled = false;");
            otherPage.Submit();
            Assert.Contains("This unit has an active lease and is no longer available.", otherPage.FormErrors);
            // Left as it was: still a draft the applicant can see.
            Assert.Equal("Draft", otherPage.Open(otherId).Status);
        }

        [Fact]
        public async Task SecondApproval_ForTheSameUnit_IsRefused()
        {
            var listing = await app.CreateListingAsync();
            using var first = app.NewBrowserAs(await app.CreateApplicantAsync("first"));
            using var second = app.NewBrowserAs(await app.CreateApplicantAsync("second"));
            var firstId = first.CreateSubmittedApplication(listing);
            var secondId = second.CreateSubmittedApplication(listing);
            using var manager = app.NewBrowserAs(await app.CreateManagerAsync());
            manager.Approve(firstId);

            var page = manager.Claim(secondId);
            page.OpenReview();
            page.FillReviewModal("Approve", null);

            manager.WaitForModalText("This unit already has an active lease.");
            Assert.Equal("Under Review", page.Open(secondId).Status);
            var leases = await app.WithDbAsync(db => db.Leases.CountAsync(l => l.Unit.PropertyId == listing.PropertyId));
            Assert.Equal(1, leases);
        }

        [Fact]
        public async Task StartingAnApplication_ForALeasedUnit_IsRefused()
        {
            var listing = await app.CreateListingAsync();
            using var winner = app.NewBrowserAs(await app.CreateApplicantAsync("winner"));
            var winnerId = winner.CreateSubmittedApplication(listing);
            using (var manager = app.NewBrowserAs(await app.CreateManagerAsync())) manager.Approve(winnerId);
            var unitId = await app.WithDbAsync(db => db.Units.Where(u => u.PropertyId == listing.PropertyId).Select(u => u.Id).SingleAsync());
            using var late = app.NewBrowserAs(await app.CreateApplicantAsync("late"));

            // It isn't listed any more, so post Apply the way the (now missing) button would.
            late.Go(UnitsPage.Path);
            Assert.False(new UnitsPage(late).Lists(listing.Name, listing.FirstUnit));
            late.Js("""
                const form = document.createElement('form');
                form.method = 'post'; form.action = '/Applications/Start';
                form.innerHTML = `<input name="unitId" value="${arguments[0]}"><input name="__RequestVerificationToken" value="${arguments[1]}">`;
                document.body.appendChild(form);
                form.submit();
                """, unitId, late.AntiforgeryToken());
            late.WaitForText("This unit is not available.");
            Assert.StartsWith(UnitsPage.Path, late.PathAndQuery);
        }
    }
}
