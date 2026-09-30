using OpenQA.Selenium;
using Troy_Web_Property_Manager.UITests.Infrastructure;
using Troy_Web_Property_Manager.UITests.Pages;
using Troy_Web_Property_Manager.UITests.Workflows;

namespace Troy_Web_Property_Manager.UITests.Tests
{
    /// <summary>
    /// Functional 4.a-4.b and 4.d, with bonus 4 (save with errors): one page showing one section at a time, driven by
    /// one form whose buttons decide what happens; Continue saves and moves on; Back doesn't save; Submit only from the
    /// Summary once both sections are saved; editable only in Draft or Returned.
    /// </summary>
    [Collection(UiCollection.Name)]
    public sealed class ApplicationEditorTests(UiFixture app)
    {
        private async Task<(Browser Browser, ApplicationPage Page)> NewDraftAsync()
        {
            var listing = await app.CreateListingAsync();
            var browser = app.NewBrowserAs(await app.CreateApplicantAsync());
            return (browser, browser.ApplyFor(listing));
        }

        [Fact]
        public async Task OnePage_ShowsOneSectionAtATime_AndContinueMovesThrough()
        {
            var (browser, page) = await NewDraftAsync();
            using var _ = browser;
            var id = page.Id;

            Assert.Equal("Applicant information", page.Section);
            Assert.True(browser.IsVisible(By.Id("ApplicantInformation_Name")));
            Assert.False(browser.IsVisible(By.Id("residence-history")));
            Assert.False(page.HasButton("Back"));
            Assert.False(page.HasButton("Submit"));

            page.FillApplicantInformation(ApplicantInfo.Valid());
            page.Continue();
            Assert.Equal("Residence history", page.Section);
            Assert.False(browser.IsVisible(By.Id("ApplicantInformation_Name")));
            Assert.True(browser.IsVisible(By.Id("residence-history")));

            page.AddResidence(ResidenceData.Valid());
            page.Continue();
            Assert.Equal("Summary", page.Section);
            // The Summary shows both sections, read-only.
            Assert.True(page.IsApplicantInformationReadOnly);
            Assert.Equal("Alex Tester", page.ReadApplicantInformation().Name);
            Assert.Single(page.Residences);
            Assert.False(page.CanAddResidence);
            // Every section is the same page for the same application.
            Assert.StartsWith(ApplicationPage.PathFor(id), browser.PathAndQuery);
        }

        [Fact]
        public async Task Continue_WithErrors_SavesAnyway_ShowsEachErrorUnderItsField_AndOffersNext()
        {
            var (browser, page) = await NewDraftAsync();
            using var _ = browser;
            var id = page.Id;

            page.FillApplicantInformation(new ApplicantInfo("", "not a phone", "", "1 Real St"));
            page.Continue();

            Assert.Equal("Applicant information", page.Section);
            Assert.Equal("Saved, but some fields still need fixing before you can submit.", new Flash(browser).Error?.Trim());
            Assert.Equal("The Name field is required.", page.FieldError("Name"));
            Assert.Equal("The Phone field is not a valid phone number.", page.FieldError("Phone"));
            Assert.Equal("The Email field is required.", page.FieldError("Email"));
            Assert.Equal("", page.FieldError("CurrentAddress"));
            Assert.True(page.HasButton("Next"));

            // It was saved: a fresh load shows the same values and the same errors.
            page.Open(id, "ApplicantInformation");
            Assert.Equal("1 Real St", page.ReadApplicantInformation().CurrentAddress);
            Assert.Equal("The Phone field is not a valid phone number.", page.FieldError("Phone"));

            page.Next();
            Assert.Equal("Residence history", page.Section);
        }

        [Fact]
        public async Task Continue_WithTextTooLongForTheDatabase_SavesNothing()
        {
            var (browser, page) = await NewDraftAsync();
            using var _ = browser;
            var id = page.Id;
            page.FillApplicantInformation(ApplicantInfo.Valid());
            page.Continue();

            page.Open(id, "ApplicantInformation");
            page.FillApplicantInformation(ApplicantInfo.Valid() with { Name = new string('x', 60) });
            page.Continue();

            Assert.Equal("The field Name must be a string with a maximum length of 50.", page.FieldError("Name"));
            Assert.Equal("Alex Tester", page.Open(id, "ApplicantInformation").ReadApplicantInformation().Name);
        }

        [Fact]
        public async Task Back_GoesToThePreviousSection_WithoutSaving()
        {
            var (browser, page) = await NewDraftAsync();
            using var _ = browser;
            var id = page.Id;
            page.FillApplicantInformation(ApplicantInfo.Valid());
            page.Continue();
            page.AddResidence(ResidenceData.Valid());

            page.Back();

            Assert.Equal("Applicant information", page.Section);
            // Residence History wasn't saved by Back, so the Summary still says so.
            page.Open(id, "Summary");
            Assert.Contains("Residence history hasn't been saved yet.", page.Blockers);
            page.Back();
            Assert.Equal("Residence history", page.Section);
        }

        [Fact]
        public async Task Summary_ListsWhatsBlockingSubmit_AndSubmitStaysDisabledUntilBothSectionsAreSaved()
        {
            var (browser, page) = await NewDraftAsync();
            using var _ = browser;
            var id = page.Id;

            page.Open(id, "Summary");
            Assert.Equal(["Applicant information hasn't been saved yet.", "Residence history hasn't been saved yet."], page.Blockers);
            Assert.False(page.IsSubmitEnabled);

            page.Open(id, "ApplicantInformation");
            page.FillApplicantInformation(ApplicantInfo.Valid() with { Phone = "nope" });
            page.Continue();
            page.Open(id, "Summary");
            Assert.Contains("Applicant information: The Phone field is not a valid phone number.", page.Blockers);
            Assert.False(page.IsSubmitEnabled);

            page.Open(id, "ApplicantInformation").CompleteSections();
            Assert.Empty(page.Blockers);
            Assert.True(page.IsSubmitEnabled);
        }

        [Fact]
        public async Task SubmittingAnApplicationWithBlockers_IsRefusedByTheServer()
        {
            var (browser, page) = await NewDraftAsync();
            using var _ = browser;
            var id = page.Id;
            page.Open(id, "Summary");

            // Re-enable the disabled Submit button, as a hand-made post would.
            browser.Js("document.querySelector(\"button[value='submit']\").disabled = false;");
            page.Submit();

            Assert.Contains("Applicant information hasn't been saved yet.", page.FormErrors);
            Assert.Equal("Draft", page.Status);
        }

        [Fact]
        public async Task Submit_SubmitsIt_AndEverySectionBecomesReadOnly()
        {
            var (browser, page) = await NewDraftAsync();
            using var _ = browser;
            var id = page.Id;
            page.CompleteSections();

            page.Submit();

            Assert.Equal("Your application was submitted.", new Flash(browser).Success?.Trim());
            Assert.Equal("Submitted", page.Status);
            Assert.Equal("Summary", page.Section);
            Assert.True(page.IsApplicantInformationReadOnly);
            Assert.False(page.CanAddResidence);
            Assert.False(page.HasButton("Submit"));
            Assert.False(page.HasButton("Add applicant"));
            Assert.True(page.HasButton("Withdraw"));

            // Read-only viewers can still flip through the sections with plain links.
            page.Back();
            Assert.Equal("Residence history", page.Section);
            Assert.False(page.CanAddResidence);
            Assert.False(page.HasButton("Continue"));
            page.Open(id, "ApplicantInformation");
            Assert.True(page.IsApplicantInformationReadOnly);
        }

        [Fact]
        public async Task Withdraw_FromADraft_MakesItFinal()
        {
            var (browser, page) = await NewDraftAsync();
            using var _ = browser;

            page.Withdraw();

            Assert.Equal("Your application was withdrawn.", new Flash(browser).Success?.Trim());
            Assert.Equal("Withdrawn", page.Status);
            Assert.False(page.HasButton("Withdraw"));
            Assert.False(page.HasButton("Continue"));
        }
    }

    /// <summary>
    /// Functional 4.c: residences are added, edited and removed through a modal, and the residence list refreshes in
    /// place (Technical 1.b). With bonus 4, a residence saves even when it breaks its rules and the modal stays open on
    /// it with the errors.
    /// </summary>
    [Collection(UiCollection.Name)]
    public sealed class ResidenceTests(UiFixture app)
    {
        private async Task<(Browser Browser, ApplicationPage Page)> OnResidenceHistoryAsync()
        {
            var listing = await app.CreateListingAsync();
            var browser = app.NewBrowserAs(await app.CreateApplicantAsync());
            var page = browser.ApplyFor(listing);
            page.FillApplicantInformation(ApplicantInfo.Valid());
            page.Continue();
            return (browser, page);
        }

        [Fact]
        public async Task AddEditAndRemoveAResidence_RefreshTheListWithoutReloadingThePage()
        {
            var (browser, page) = await OnResidenceHistoryAsync();
            using var _ = browser;
            browser.MarkPage();

            page.AddResidence(ResidenceData.Valid("5 Elm St"));
            Assert.Equal(new ResidenceRow("5 Elm St", "Pat Landlord", "518-555-0199", "1/1/2020", "12/31/2024"), page.Residences.Single());

            page.EditResidence("5 Elm St", new ResidenceData("6 Pine St", "Sam Owner", null, null, null));
            browser.WaitUntil(() => page.Residences.Single().Address == "6 Pine St", "the edited residence");
            Assert.Equal("Sam Owner", page.Residences.Single().Landlord);

            page.RemoveResidence("6 Pine St");
            Assert.Empty(page.Residences);
            Assert.True(browser.IsSamePage(), "The residence modals reloaded the page instead of redrawing the list.");
        }

        [Fact]
        public async Task ResidenceThatBreaksTheRules_IsSaved_AndTheModalStaysOpenWithTheErrors()
        {
            var (browser, page) = await OnResidenceHistoryAsync();
            using var _ = browser;

            page.OpenAddResidence();
            page.FillResidenceModal(new ResidenceData("7 Oak St", "Lee Landlord", "not a phone", "2024-06-01", "2023-01-01"));

            browser.WaitForModalText("Saved, but some fields still need fixing before you can submit.");
            Assert.Contains("Move-out date must be on or after the move-in date.", browser.ModalText);
            Assert.Contains("The Landlord phone field is not a valid phone number.", browser.ModalText);
            Assert.Equal("Edit residence", browser.ModalTitle());
            // The list behind the modal already shows it, with its errors under the row.
            browser.WaitUntil(() => page.Residences.Count == 1, "the saved residence in the list");
            Assert.Contains("Move-out date must be on or after the move-in date.", page.ResidenceErrors);

            // Fixing it in the same modal edits that residence instead of adding a second one.
            page.FillResidenceModal(new ResidenceData(null, null, "518-555-0123", "2022-01-01", null));
            browser.WaitForModalClosed();
            browser.WaitUntil(() => page.ResidenceErrors.Count == 0, "the errors to clear");
            Assert.Single(page.Residences);
        }

        [Fact]
        public async Task EmptyResidence_IsSavedWithAnErrorForEveryRequiredField()
        {
            var (browser, page) = await OnResidenceHistoryAsync();
            using var _ = browser;

            page.OpenAddResidence();
            page.FillResidenceModal(new ResidenceData(null, null, null, null, null));

            browser.WaitForModalText("Saved, but some fields still need fixing before you can submit.");
            foreach (var field in new[] { "Address", "Landlord name", "Landlord phone", "Move-in date", "Move-out date" })
            {
                Assert.Contains($"The {field} field is required.", browser.ModalText);
            }
        }

        [Fact]
        public async Task RemovingTheLastResidence_LeavesTheSectionSavedButBlocked()
        {
            var (browser, page) = await OnResidenceHistoryAsync();
            using var _ = browser;
            var id = page.Id;
            page.AddResidence(ResidenceData.Valid("8 Last St"));
            page.Continue();

            page.Open(id, "ResidenceHistory").RemoveResidence("8 Last St");
            page.Open(id, "ResidenceHistory");

            Assert.Contains("Add at least one prior residence.", page.ResidenceHistoryErrors);
            page.Open(id, "Summary");
            Assert.Contains("Add at least one prior residence.", page.Blockers);
            Assert.False(page.IsSubmitEnabled);
        }
    }
}
