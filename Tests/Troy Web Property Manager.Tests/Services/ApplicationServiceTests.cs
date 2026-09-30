using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Rules;
using Troy_Web_Property_Manager.Services;
using Troy_Web_Property_Manager.ViewModels;
using static Troy_Web_Property_Manager.Tests.TestDatabase;

namespace Troy_Web_Property_Manager.Tests.Services
{
    public sealed class ApplicationServiceTests : IDisposable
    {
        private readonly TestDatabase _db = new();

        public void Dispose()
        {
            _db.Dispose();
        }

        // ---------------- Helpers ----------------

        /// <summary>A service on a fresh context, same as each web request would get.</summary>
        private ApplicationService Service(params IInterceptor[] interceptors)
        {
            return new(_db.CreateContext(interceptors));
        }

        private static void AssertOk(ServiceResult result)
        {
            Assert.True(result.Succeeded, $"NotFound={result.NotFound}; {string.Join("; ", result.Errors.Values)}");
        }

        private static void AssertError(ServiceResult result, string expectedMessagePart)
        {
            Assert.False(result.Succeeded);
            Assert.False(result.NotFound);
            Assert.Contains(result.Errors.Values, m => m.Contains(expectedMessagePart));
        }

        private static ApplicantInformationViewModel Info(string email = "alex@example.com")
        {
            return new()
            {
                Name = " Alex ApplicantUser ",
                Phone = "518-555-0100",
                Email = email,
                CurrentAddress = "9 Oak Ave"
            };
        }

        private static ResidenceViewModel Residence()
        {
            return new()
            {
                Address = "5 Elm St",
                LandlordName = "Pat Landlord",
                LandlordPhone = "518-555-0199",
                MoveInDate = new DateOnly(2020, 1, 1),
                MoveOutDate = new DateOnly(2024, 12, 31)
            };
        }

        private static ReviewViewModel Review(ReviewOutcome outcome, string? comment = null)
        {
            return new() { Outcome = outcome, Comment = comment };
        }

        // ---------------- Section saves at the current version ----------------
        // Most tests aren't about concurrency, so these save against whatever version is current - like a page that
        // was just loaded. The concurrency tests call the service directly with the versions they want.

        private async Task<(Guid Info, Guid History)> VersionsAsync(int id)
        {
            var versions = await _db.CreateContext().RentalApplications.Where(a => a.Id == id)
                .Select(a => new { a.ApplicantInformationVersion, a.ResidenceHistoryVersion }).FirstOrDefaultAsync();
            return versions is null ? (Guid.Empty, Guid.Empty) : (versions.ApplicantInformationVersion, versions.ResidenceHistoryVersion);
        }

        private async Task<ServiceResult> SaveInfoAsync(int id, ApplicantInformationViewModel model, CurrentUser user)
        {
            return await Service().SaveApplicantInformationAsync(id, model, (await VersionsAsync(id)).Info, user);
        }

        private async Task<ServiceResult> SaveHistoryAsync(int id, CurrentUser user)
        {
            return await Service().SaveResidenceHistoryAsync(id, (await VersionsAsync(id)).History, user);
        }

        private async Task<ServiceResult> SaveResidenceAsync(int id, ResidenceViewModel model, CurrentUser user)
        {
            model.SectionVersion = (await VersionsAsync(id)).History;
            return await Service().SaveResidenceAsync(id, model, user);
        }

        private async Task<ServiceResult> DeleteResidenceAsync(int id, int residenceId, CurrentUser user)
        {
            return await Service().DeleteResidenceAsync(id, residenceId, (await VersionsAsync(id)).History, user);
        }

        private async Task<ServiceResult> SubmitAsync(int id, CurrentUser user)
        {
            var (info, history) = await VersionsAsync(id);
            return await Service().SubmitAsync(id, info, history, user);
        }

        private async Task<int> StartAsync(CurrentUser? user = null, int? unitId = null)
        {
            var result = await Service().StartAsync(unitId ?? _db.UnitId, user ?? ApplicantUser);
            AssertOk(result);
            return result.Id;
        }

        /// <summary>A draft with both sections saved, ready to submit.</summary>
        private async Task<int> CompleteDraftAsync(CurrentUser? user = null, int? unitId = null)
        {
            user ??= ApplicantUser;
            var id = await StartAsync(user, unitId);
            AssertOk(await SaveInfoAsync(id, Info(), user));
            AssertOk(await SaveResidenceAsync(id, Residence(), user));
            AssertOk(await SaveHistoryAsync(id, user));
            return id;
        }

        private async Task<int> SubmittedAsync(CurrentUser? user = null, int? unitId = null)
        {
            user ??= ApplicantUser;
            var id = await CompleteDraftAsync(user, unitId);
            AssertOk(await SubmitAsync(id, user));
            return id;
        }

        /// <summary>Submitted and then claimed from the review queue, so <paramref name="manager"/> can review it.</summary>
        private async Task<int> ClaimedAsync(CurrentUser? user = null, int? unitId = null, CurrentUser? manager = null)
        {
            var id = await SubmittedAsync(user, unitId);
            AssertOk(await Service().ClaimAsync(id, manager ?? ManagerUser));
            return id;
        }

        /// <summary>Gives the unit an active lease by approving another applicant's application for it.</summary>
        private async Task LeaseUnitAsync(int unitId)
        {
            var id = await ClaimedAsync(OtherApplicantUser, unitId);
            AssertOk(await Service().ReviewAsync(id, Review(ReviewOutcome.Approve), ManagerUser));
        }

        private Task<RentalApplication> LoadAsync(int id)
        {
            return _db.CreateContext().RentalApplications
                .Include(a => a.ApplicationStatusHistories)
                .Include(a => a.ApplicantInformation)
                .Include(a => a.Leases)
                .SingleAsync(a => a.Id == id);
        }

        /// <summary>
        /// Fakes another request changing the status after the service loaded the application but before it saves.
        /// Pass a <paramref name="reviewer"/> with Under Review to fake another manager's claim; otherwise the claim
        /// is cleared, the same as the app does (the check constraint insists on it).
        /// </summary>
        private sealed class ChangeStatusBeforeSave(int applicationId, ApplicationStatus status, string? reviewer = null) : SaveChangesInterceptor
        {
            public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                DateTime? claimed = reviewer is null ? null : DateTime.Now;
                await eventData.Context!.Database.ExecuteSqlAsync(
                    $"UPDATE RentalApplications SET Status = {(long)status}, ReviewerUser = {reviewer}, ReviewClaimed = {claimed} WHERE id = {applicationId}",
                    cancellationToken);
                return result;
            }
        }

        // ---------------- Start ----------------

        [Fact]
        public async Task Start_CreatesDraftWithHistoryAndApplicantProfile()
        {
            var id = await StartAsync();

            var application = await LoadAsync(id);
            Assert.Equal((long)ApplicationStatus.Draft, application.Status);
            Assert.Null(application.Submitted);
            var history = Assert.Single(application.ApplicationStatusHistories);
            Assert.Equal((long)ApplicationStatus.Draft, history.NewStatus);
            Assert.Equal(ApplicantUser.Id, history.ChangedByUser);

            var profile = await _db.CreateContext().Applicants.SingleAsync();
            Assert.Equal(ApplicantUser.Id, profile.UserId);
            Assert.Equal("applicant-1@example.com", profile.Email);
        }

        [Fact]
        public async Task Start_AgainForSameUnit_ReopensOpenApplication()
        {
            var draft = await StartAsync();
            Assert.Equal(draft, await StartAsync());

            AssertOk(await SaveInfoAsync(draft, Info(), ApplicantUser));
            AssertOk(await SaveResidenceAsync(draft, Residence(), ApplicantUser));
            AssertOk(await SaveHistoryAsync(draft, ApplicantUser));
            AssertOk(await SubmitAsync(draft, ApplicantUser));
            Assert.Equal(draft, await StartAsync());

            Assert.Equal(1, await _db.CreateContext().RentalApplications.CountAsync());
        }

        [Fact]
        public async Task Start_AfterWithdrawing_CreatesNewApplication()
        {
            var first = await StartAsync();
            AssertOk(await Service().WithdrawAsync(first, ApplicantUser));

            Assert.NotEqual(first, await StartAsync());
        }

        [Fact]
        public async Task Start_ForAnotherUnit_ReusesApplicantProfile()
        {
            await StartAsync(unitId: _db.UnitId);
            await StartAsync(unitId: _db.SecondUnitId);

            Assert.Equal(1, await _db.CreateContext().Applicants.CountAsync());
        }

        [Fact]
        public async Task Start_ByManager_IsRejected()
        {
            var result = await Service().StartAsync(_db.UnitId, ManagerUser);
            AssertError(result, "Only applicants");
            Assert.True(result.Forbidden);
        }

        [Fact]
        public async Task Start_UnknownUnit_IsNotFound()
        {
            Assert.True((await Service().StartAsync(9999, ApplicantUser)).NotFound);
        }

        [Fact]
        public async Task Start_LeasedUnit_IsRejected()
        {
            await LeaseUnitAsync(_db.UnitId);
            AssertError(await Service().StartAsync(_db.UnitId, ApplicantUser), "not available");
        }

        [Fact]
        public async Task Database_RejectsSecondOpenApplicationForSameApplicantAndUnit()
        {
            var id = await StartAsync();
            var db = _db.CreateContext();
            var existing = await db.RentalApplications.AsNoTracking().SingleAsync(a => a.Id == id);
            db.RentalApplications.Add(new RentalApplication
            {
                UnitId = existing.UnitId,
                ApplicantId = existing.ApplicantId,
                Status = (long)ApplicationStatus.Draft,
                Created = DateTime.Now
            });

            // The filtered unique index is what stops a double-clicked Apply from creating two drafts.
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        // ---------------- Editor and ownership ----------------

        [Fact]
        public async Task GetEditor_OtherApplicantsApplication_IsHidden()
        {
            var id = await StartAsync();
            Assert.Null(await Service().GetEditorAsync(id, null, OtherApplicantUser));
        }

        [Fact]
        public async Task GetEditor_Draft_OpensEditableFirstSection()
        {
            var id = await StartAsync();
            var editor = await Service().GetEditorAsync(id, null, ApplicantUser);

            Assert.NotNull(editor);
            Assert.True(editor.CanEdit);
            Assert.False(editor.IsReadOnly);
            Assert.Equal(ApplicationSection.ApplicantInformation, editor.Section);
            Assert.False(editor.CanSubmit);
        }

        [Fact]
        public async Task GetEditor_Manager_SeesSummaryReadOnly()
        {
            var id = await SubmittedAsync();
            var editor = await Service().GetEditorAsync(id, null, ManagerUser);

            Assert.NotNull(editor);
            Assert.False(editor.CanEdit);
            Assert.True(editor.IsReadOnly);
            Assert.Equal(ApplicationSection.Summary, editor.Section);
            // Waiting in the queue: it has to be claimed before it can be reviewed.
            Assert.True(editor.CanClaim);
            Assert.False(editor.CanReview);
            Assert.False(editor.CanRelease);
            Assert.False(editor.CanWithdraw);
        }

        [Fact]
        public async Task GetEditor_NewApplication_IsPrefilledFromLastSavedDetails()
        {
            var first = await StartAsync(unitId: _db.UnitId);
            AssertOk(await SaveInfoAsync(first, Info("first@example.com"), ApplicantUser));

            var second = await StartAsync(unitId: _db.SecondUnitId);
            var editor = await Service().GetEditorAsync(second, null, ApplicantUser);

            Assert.Equal("first@example.com", editor!.ApplicantInformation.Email);
            Assert.Equal("Alex ApplicantUser", editor.ApplicantInformation.Name); // trimmed when saved
        }

        [Fact]
        public async Task SaveApplicantInformation_IsKeptPerApplication()
        {
            var submitted = await SubmittedAsync(unitId: _db.UnitId);
            var draft = await StartAsync(unitId: _db.SecondUnitId);
            AssertOk(await SaveInfoAsync(draft, Info("changed@example.com"), ApplicantUser));

            // The submitted application keeps exactly what was submitted.
            Assert.Equal("alex@example.com", (await LoadAsync(submitted)).ApplicantInformation!.Email);
            Assert.Equal("changed@example.com", (await LoadAsync(draft)).ApplicantInformation!.Email);
        }

        // ---------------- Sections and submit ----------------

        [Fact]
        public async Task SaveResidenceHistory_WithoutResidences_SavesWithTheError()
        {
            var id = await StartAsync();

            var result = await SaveHistoryAsync(id, ApplicantUser);

            AssertOk(result);
            var error = Assert.Single(result.Unresolved);
            Assert.Equal((ApplicationService.ResidencesKey, SubmissionRules.NoResidences), (error.Field, error.Message));
            Assert.True((await LoadAsync(id)).ResidenceHistorySaved);
            var editor = await Service().GetEditorAsync(id, ApplicationSection.ResidenceHistory, ApplicantUser);
            Assert.Equal(SubmissionRules.NoResidences, Assert.Single(editor!.ResidenceHistoryErrors).Message);
            Assert.True(editor.SectionHasErrors(ApplicationSection.ResidenceHistory));
        }

        // ---------------- Saving with errors ----------------

        private static ApplicantInformationViewModel BadInfo()
        {
            return new() { Name = "  ", Phone = "not a phone", Email = "nope", CurrentAddress = "9 Oak Ave" };
        }

        [Fact]
        public async Task SaveApplicantInformation_WithErrors_SavesAndReturnsThemByField()
        {
            var id = await StartAsync();

            var result = await SaveInfoAsync(id, BadInfo(), ApplicantUser);

            AssertOk(result);
            Assert.Equal(new[] { "Email", "Name", "Phone" }, result.Unresolved.Select(e => e.Field).Order());
            var application = await LoadAsync(id);
            Assert.True(application.ApplicantInformationSaved);
            Assert.Equal("", application.ApplicantInformation!.Name); // blank stored as ""
            Assert.Equal("not a phone", application.ApplicantInformation.Phone);
        }

        [Fact]
        public async Task Editor_PutsSavedErrorsOnTheirFields_AndSummaryListsThem()
        {
            var id = await StartAsync();
            AssertOk(await SaveInfoAsync(id, BadInfo(), ApplicantUser));

            var editor = await Service().GetEditorAsync(id, ApplicationSection.Summary, ApplicantUser);

            Assert.Contains(editor!.ApplicantInformationErrors, e => e.Field == "ApplicantInformation.Phone");
            Assert.Contains(editor.ApplicantInformationErrors, e => e.Field == "ApplicantInformation.Email");
            Assert.Contains(editor.ApplicantInformationErrors, e => e.Field == "ApplicantInformation.Name");
            Assert.True(editor.SectionHasErrors(ApplicationSection.ApplicantInformation));
            Assert.Contains(editor.SubmitBlockers, b => b.StartsWith("Applicant information:") && b.Contains("Phone"));
            Assert.Contains(SubmissionRules.ResidenceHistoryNotSaved, editor.SubmitBlockers);
            Assert.False(editor.CanSubmit);
        }

        [Fact]
        public async Task Submit_IsBlockedWhileAnyErrorRemains_ThenWorksOnceFixed()
        {
            var id = await CompleteDraftAsync();
            AssertOk(await SaveInfoAsync(id, BadInfo(), ApplicantUser));

            var result = await SubmitAsync(id, ApplicantUser);
            AssertError(result, "Applicant information:");
            Assert.Equal((long)ApplicationStatus.Draft, (await LoadAsync(id)).Status);

            AssertOk(await SaveInfoAsync(id, Info(), ApplicantUser));
            AssertOk(await SubmitAsync(id, ApplicantUser));
        }

        [Fact]
        public async Task SaveApplicantInformation_WithErrors_DoesNotOverwriteTheProfileUsedForPrefill()
        {
            var first = await StartAsync(unitId: _db.UnitId);
            AssertOk(await SaveInfoAsync(first, Info("good@example.com"), ApplicantUser));
            AssertOk(await SaveInfoAsync(first, BadInfo(), ApplicantUser));

            var second = await StartAsync(unitId: _db.SecondUnitId);
            Assert.Equal("good@example.com", (await Service().GetEditorAsync(second, null, ApplicantUser))!.ApplicantInformation.Email);
        }

        [Fact]
        public async Task SaveApplicantInformation_TooLongForTheDatabase_IsNotSaved()
        {
            var id = await StartAsync();
            var info = Info();
            info.Name = new string('x', 51);

            var result = await SaveInfoAsync(id, info, ApplicantUser);

            Assert.False(result.Succeeded);
            Assert.True(result.Errors.ContainsKey("Name"));
            Assert.False((await LoadAsync(id)).ApplicantInformationSaved);
        }

        [Fact]
        public async Task SaveResidence_WithErrors_SavesAndReturnsItsIdAndErrors()
        {
            var id = await StartAsync();
            var residence = Residence();
            residence.LandlordPhone = "";
            residence.MoveInDate = new DateOnly(2025, 1, 1); // after the move-out date
            residence.MoveOutDate = new DateOnly(2024, 1, 1);

            var result = await SaveResidenceAsync(id, residence, ApplicantUser);

            AssertOk(result);
            Assert.NotEqual(0, result.Id);
            Assert.Equal(new[] { nameof(ResidenceViewModel.LandlordPhone), nameof(ResidenceViewModel.MoveOutDate) },
                result.Unresolved.Select(e => e.Field).Order());
            // Saving again with the returned id edits the same residence rather than adding another.
            residence.ResidenceId = result.Id;
            residence.LandlordPhone = "518-555-0199";
            residence.MoveOutDate = new DateOnly(2025, 6, 1);
            var fixedResult = await SaveResidenceAsync(id, residence, ApplicantUser);
            AssertOk(fixedResult);
            Assert.Empty(fixedResult.Unresolved);
            Assert.Single(_db.CreateContext().Residences.Where(r => r.RentalApplicationId == id));
        }

        [Fact]
        public async Task SaveResidence_WithNoDates_IsSavedAndReportsBoth()
        {
            var id = await StartAsync();

            var result = await SaveResidenceAsync(id, new ResidenceViewModel { Address = "5 Elm St" }, ApplicantUser);

            AssertOk(result);
            Assert.Contains(result.Unresolved, e => e.Field == nameof(ResidenceViewModel.MoveInDate));
            Assert.Contains(result.Unresolved, e => e.Field == nameof(ResidenceViewModel.MoveOutDate));
            var saved = await _db.CreateContext().Residences.SingleAsync(r => r.RentalApplicationId == id);
            Assert.Null(saved.MoveInDate);
            Assert.Equal("", saved.LandlordName);
        }

        [Fact]
        public async Task ResidenceErrors_ShowOnTheRowAndInTheModal_AndBlockSubmit()
        {
            var id = await CompleteDraftAsync();
            var residence = Residence();
            residence.LandlordName = null;
            var residenceId = (await SaveResidenceAsync(id, residence, ApplicantUser)).Id;

            var editor = await Service().GetEditorAsync(id, ApplicationSection.ResidenceHistory, ApplicantUser);
            var row = editor!.Residences.Single(r => r.ResidenceId == residenceId);
            Assert.Equal(nameof(ResidenceViewModel.LandlordName), Assert.Single(row.Errors).Field);
            Assert.Contains(editor.SubmitBlockers, b => b.StartsWith("Residence history: 5 Elm St - ") && b.Contains("Landlord name"));
            Assert.Equal(nameof(ResidenceViewModel.LandlordName),
                Assert.Single((await Service().GetResidenceAsync(id, residenceId, ApplicantUser))!.Errors).Field);
            AssertError(await SubmitAsync(id, ApplicantUser), "Landlord name");
        }

        [Fact]
        public async Task SaveResidenceHistory_ReturnsTheResidencesErrors()
        {
            var id = await StartAsync();
            var residence = Residence();
            residence.LandlordPhone = null;
            AssertOk(await SaveResidenceAsync(id, residence, ApplicantUser));

            var result = await SaveHistoryAsync(id, ApplicantUser);

            AssertOk(result);
            Assert.Equal(nameof(ResidenceViewModel.LandlordPhone), Assert.Single(result.Unresolved).Field);
        }

        [Fact]
        public async Task CleanSections_HaveNoErrors()
        {
            var id = await CompleteDraftAsync();

            var editor = await Service().GetEditorAsync(id, ApplicationSection.Summary, ApplicantUser);

            Assert.Empty(editor!.ApplicantInformationErrors);
            Assert.Empty(editor.ResidenceHistoryErrors);
            Assert.All(editor.Residences, r => Assert.Empty(r.Errors));
            Assert.Empty(editor.SubmitBlockers);
            Assert.True(editor.CanSubmit);
        }

        [Fact]
        public async Task Managers_NeverGetFieldErrors()
        {
            var id = await SubmittedAsync();

            var editor = await Service().GetEditorAsync(id, null, ManagerUser);

            Assert.Empty(editor!.ApplicantInformationErrors);
            Assert.Empty(editor.ResidenceHistoryErrors);
            Assert.Empty(editor.SubmitBlockers);
        }

        [Fact]
        public async Task SaveResidence_OnOtherApplicantsApplication_IsNotFound()
        {
            var id = await StartAsync();
            Assert.True((await SaveResidenceAsync(id, Residence(), OtherApplicantUser)).NotFound);
        }

        [Fact]
        public async Task DeleteLastResidence_LeavesTheSectionSavedWithTheNoResidencesError()
        {
            var id = await CompleteDraftAsync();
            var residenceId = (await Service().GetEditorAsync(id, null, ApplicantUser))!.Residences.Single().ResidenceId!.Value;

            AssertOk(await DeleteResidenceAsync(id, residenceId, ApplicantUser));

            Assert.True((await LoadAsync(id)).ResidenceHistorySaved);
            AssertError(await SubmitAsync(id, ApplicantUser), SubmissionRules.NoResidences);
        }

        [Fact]
        public async Task Submit_BeforeBothSectionsSaved_IsRejected()
        {
            var id = await StartAsync();
            AssertOk(await SaveInfoAsync(id, Info(), ApplicantUser));

            AssertError(await SubmitAsync(id, ApplicantUser), SubmissionRules.ResidenceHistoryNotSaved);
        }

        [Fact]
        public async Task Submit_CompleteDraft_BecomesSubmittedWithHistory()
        {
            var id = await SubmittedAsync();

            var application = await LoadAsync(id);
            Assert.Equal((long)ApplicationStatus.Submitted, application.Status);
            Assert.NotNull(application.Submitted);
            Assert.Contains(application.ApplicationStatusHistories,
                h => h.PreviousStatus == (long)ApplicationStatus.Draft && h.NewStatus == (long)ApplicationStatus.Submitted);
        }

        [Fact]
        public async Task Submit_WhenUnitHasActiveLease_IsRejected()
        {
            var id = await CompleteDraftAsync();
            await LeaseUnitAsync(_db.UnitId);

            AssertError(await SubmitAsync(id, ApplicantUser), SubmissionRules.UnitLeased);
            // The Summary shows the same reason and keeps Submit disabled.
            var editor = (await Service().GetEditorAsync(id, ApplicationSection.Summary, ApplicantUser))!;
            Assert.Equal([SubmissionRules.UnitLeased], editor.SubmitBlockers);
            Assert.False(editor.CanSubmit);
        }

        [Fact]
        public async Task Edits_AfterSubmit_AreRejected()
        {
            var id = await SubmittedAsync();

            AssertError(await SaveInfoAsync(id, Info(), ApplicantUser), "can no longer be edited");
            AssertError(await SaveResidenceAsync(id, Residence(), ApplicantUser), "can no longer be edited");
            AssertError(await SubmitAsync(id, ApplicantUser), "can no longer be edited");
        }

        [Fact]
        public async Task SaveResidence_WhenSubmittedConcurrently_IsRejected()
        {
            var id = await CompleteDraftAsync();

            var residence = Residence();
            residence.SectionVersion = (await VersionsAsync(id)).History;
            var result = await Service(new ChangeStatusBeforeSave(id, ApplicationStatus.Submitted)).SaveResidenceAsync(id, residence, ApplicantUser);

            Assert.True(result.Conflict);
            Assert.Single(_db.CreateContext().Residences.Where(r => r.RentalApplicationId == id));
        }

        [Fact]
        public async Task SaveApplicantInformation_AlreadySavedAndSubmittedConcurrently_IsRejected()
        {
            // Section 1 is already saved, so this save doesn't change the application row itself.
            var id = await CompleteDraftAsync();

            var result = await Service(new ChangeStatusBeforeSave(id, ApplicationStatus.Submitted))
                .SaveApplicantInformationAsync(id, Info("changed@example.com"), (await VersionsAsync(id)).Info, ApplicantUser);

            Assert.True(result.Conflict);
            Assert.NotEqual("changed@example.com", _db.CreateContext().ApplicantInformation.Single(i => i.RentalApplicationId == id).Email);
        }

        [Fact]
        public async Task DeleteResidence_WhenSubmittedConcurrently_IsRejected()
        {
            var id = await CompleteDraftAsync();
            AssertOk(await SaveResidenceAsync(id, Residence(), ApplicantUser));
            var residenceId = (await Service().GetEditorAsync(id, null, ApplicantUser))!.Residences.First().ResidenceId!.Value;

            var result = await Service(new ChangeStatusBeforeSave(id, ApplicationStatus.Submitted))
                .DeleteResidenceAsync(id, residenceId, (await VersionsAsync(id)).History, ApplicantUser);

            Assert.True(result.Conflict);
            Assert.Equal(2, _db.CreateContext().Residences.Count(r => r.RentalApplicationId == id));
        }

        // ---------------- Withdraw ----------------

        [Fact]
        public async Task Withdraw_Submitted_Succeeds()
        {
            var id = await SubmittedAsync();
            AssertOk(await Service().WithdrawAsync(id, ApplicantUser));

            Assert.Equal((long)ApplicationStatus.Withdrawn, (await LoadAsync(id)).Status);
        }

        [Fact]
        public async Task Withdraw_Approved_IsRejected()
        {
            var id = await ClaimedAsync();
            AssertOk(await Service().ReviewAsync(id, Review(ReviewOutcome.Approve), ManagerUser));

            AssertError(await Service().WithdrawAsync(id, ApplicantUser), "can't be withdrawn");
        }

        [Fact]
        public async Task Withdraw_ByManager_IsRejected()
        {
            var id = await SubmittedAsync();
            AssertError(await Service().WithdrawAsync(id, ManagerUser), "can't be withdrawn");
        }

        [Fact]
        public async Task Withdraw_OtherApplicantsApplication_IsNotFound()
        {
            var id = await SubmittedAsync();
            Assert.True((await Service().WithdrawAsync(id, OtherApplicantUser)).NotFound);
        }

        [Fact]
        public async Task Withdraw_WhenApprovedConcurrently_DoesNotOverwriteApproval()
        {
            var id = await SubmittedAsync();

            var result = await Service(new ChangeStatusBeforeSave(id, ApplicationStatus.Approved)).WithdrawAsync(id, ApplicantUser);

            AssertError(result, "changed by someone else");
            Assert.True(result.Conflict);
            var application = await LoadAsync(id);
            Assert.Equal((long)ApplicationStatus.Approved, application.Status);
            Assert.DoesNotContain(application.ApplicationStatusHistories, h => h.NewStatus == (long)ApplicationStatus.Withdrawn);
        }

        // ---------------- Review ----------------

        [Fact]
        public async Task CanReview_OnlyTheManagerWhoClaimedIt()
        {
            var draft = await StartAsync(unitId: _db.SecondUnitId);
            var claimed = await ClaimedAsync(unitId: _db.UnitId);
            var waiting = await SubmittedAsync(OtherApplicantUser, _db.SecondUnitId);

            Assert.True(await Service().CanReviewAsync(claimed, ManagerUser));
            Assert.False(await Service().CanReviewAsync(claimed, OtherManagerUser));
            Assert.False(await Service().CanReviewAsync(claimed, ApplicantUser));
            Assert.False(await Service().CanReviewAsync(waiting, ManagerUser)); // not claimed yet
            Assert.False(await Service().CanReviewAsync(draft, ManagerUser));
            Assert.False(await Service().CanReviewAsync(9999, ManagerUser));
        }

        [Fact]
        public async Task Review_Approve_CreatesTwelveMonthLease()
        {
            var id = await ClaimedAsync();

            AssertOk(await Service().ReviewAsync(id, Review(ReviewOutcome.Approve), ManagerUser));

            var application = await LoadAsync(id);
            Assert.Equal((long)ApplicationStatus.Approved, application.Status);
            var lease = Assert.Single(application.Leases);
            Assert.Equal(_db.UnitId, lease.UnitId);
            Assert.Equal(DateTime.Today, lease.StartDate);
            Assert.Equal(DateTime.Today.AddMonths(LeaseRules.TermMonths), lease.EndDate);
            Assert.Contains(application.ApplicationStatusHistories,
                h => h.Outcome == ReviewOutcome.Approve && h.ChangedByUser == ManagerUser.Id);
        }

        [Fact]
        public async Task Review_Approve_RemovesUnitFromAvailableUnits()
        {
            var id = await ClaimedAsync();
            AssertOk(await Service().ReviewAsync(id, Review(ReviewOutcome.Approve), ManagerUser));

            var available = await new PropertyService(_db.CreateContext()).GetAvailableUnitsAsync();
            Assert.Equal(new[] { _db.SecondUnitId }, available.Select(u => u.Id));
        }

        [Fact]
        public async Task Review_Return_ShowsCommentToApplicantAndReopensEditing()
        {
            var id = await ClaimedAsync();

            AssertOk(await Service().ReviewAsync(id, Review(ReviewOutcome.Return, " Add a second reference. "), ManagerUser));

            var editor = await Service().GetEditorAsync(id, null, ApplicantUser);
            Assert.Equal(ApplicationStatus.Returned, editor!.Status);
            Assert.Equal("Add a second reference.", editor.ReviewComment);
            Assert.True(editor.CanEdit);
            Assert.True(editor.CanSubmit); // both sections are still saved, so it can be resubmitted as is
            AssertOk(await SubmitAsync(id, ApplicantUser));

            // Resubmitted, it goes back in the queue unclaimed.
            var application = await LoadAsync(id);
            Assert.Equal((long)ApplicationStatus.Submitted, application.Status);
            Assert.Null(application.ReviewerUser);
            Assert.Null(application.ReviewClaimed);
        }

        [Fact]
        public async Task Review_Deny_ShowsCommentToApplicant()
        {
            var id = await ClaimedAsync();
            AssertOk(await Service().ReviewAsync(id, Review(ReviewOutcome.Deny, "Income too low."), ManagerUser));

            var editor = await Service().GetEditorAsync(id, null, ApplicantUser);
            Assert.Equal(ApplicationStatus.Denied, editor!.Status);
            Assert.Equal("Income too low.", editor.ReviewComment);
            Assert.False(editor.CanEdit);
            Assert.False(editor.CanWithdraw);
        }

        [Theory]
        [InlineData(ReviewOutcome.Return)]
        [InlineData(ReviewOutcome.Deny)]
        public async Task Review_ReturnOrDenyWithoutComment_IsRejected(ReviewOutcome outcome)
        {
            var id = await ClaimedAsync();

            var result = await Service().ReviewAsync(id, Review(outcome, "  "), ManagerUser);

            AssertError(result, "comment is required");
            Assert.True(result.Errors.ContainsKey(nameof(ReviewViewModel.Comment)));
            Assert.Equal((long)ApplicationStatus.UnderReview, (await LoadAsync(id)).Status);
        }

        [Fact]
        public async Task Review_ByApplicant_IsRejected()
        {
            var id = await ClaimedAsync();
            var result = await Service().ReviewAsync(id, Review(ReviewOutcome.Approve), ApplicantUser);
            AssertError(result, "Only property managers");
            Assert.True(result.Forbidden);
        }

        [Fact]
        public async Task Review_Draft_IsRejected()
        {
            var id = await CompleteDraftAsync();
            AssertError(await Service().ReviewAsync(id, Review(ReviewOutcome.Approve), ManagerUser), "Claim this application");
        }

        [Fact]
        public async Task Review_ApproveSecondApplicationForSameUnit_IsRejected()
        {
            var first = await ClaimedAsync(ApplicantUser);
            var second = await ClaimedAsync(OtherApplicantUser);
            AssertOk(await Service().ReviewAsync(first, Review(ReviewOutcome.Approve), ManagerUser));

            AssertError(await Service().ReviewAsync(second, Review(ReviewOutcome.Approve), ManagerUser), "already has an active lease");

            Assert.Equal(1, await _db.CreateContext().Leases.CountAsync());
            Assert.Equal((long)ApplicationStatus.UnderReview, (await LoadAsync(second)).Status);
        }

        [Fact]
        public async Task Review_WhenWithdrawnConcurrently_CreatesNoLease()
        {
            var id = await ClaimedAsync();

            var result = await Service(new ChangeStatusBeforeSave(id, ApplicationStatus.Withdrawn))
                .ReviewAsync(id, Review(ReviewOutcome.Approve), ManagerUser);

            AssertError(result, "changed by someone else");
            Assert.True(result.Conflict);
            Assert.Equal(0, await _db.CreateContext().Leases.CountAsync());
        }

        // ---------------- Review queue (claim / release) ----------------

        [Fact]
        public async Task Claim_MovesItUnderReviewAndRecordsWhoClaimedIt()
        {
            var id = await SubmittedAsync();
            var submittedAt = (await LoadAsync(id)).Submitted;

            AssertOk(await Service().ClaimAsync(id, ManagerUser));

            var application = await LoadAsync(id);
            Assert.Equal((long)ApplicationStatus.UnderReview, application.Status);
            Assert.Equal(ManagerUser.Id, application.ReviewerUser);
            Assert.NotNull(application.ReviewClaimed);
            Assert.Equal(submittedAt, application.Submitted); // still the applicant's submit time
            Assert.Contains(application.ApplicationStatusHistories, h => h.PreviousStatus == (long)ApplicationStatus.Submitted
                && h.NewStatus == (long)ApplicationStatus.UnderReview && h.ChangedByUser == ManagerUser.Id);
        }

        [Fact]
        public async Task Claim_ByApplicant_IsForbidden()
        {
            var id = await SubmittedAsync();

            var result = await Service().ClaimAsync(id, ApplicantUser);

            Assert.True(result.Forbidden);
            Assert.Equal((long)ApplicationStatus.Submitted, (await LoadAsync(id)).Status);
        }

        [Fact]
        public async Task Claim_AlreadyClaimed_IsRejectedAndKeepsTheFirstClaim()
        {
            var id = await ClaimedAsync(manager: ManagerUser);

            AssertError(await Service().ClaimAsync(id, OtherManagerUser), "already been claimed");

            Assert.Equal(ManagerUser.Id, (await LoadAsync(id)).ReviewerUser);
        }

        [Fact]
        public async Task Claim_OnlySubmittedApplications()
        {
            var draft = await StartAsync(unitId: _db.SecondUnitId);
            var returned = await ClaimedAsync(unitId: _db.UnitId);
            AssertOk(await Service().ReviewAsync(returned, Review(ReviewOutcome.Return, "Fix it."), ManagerUser));

            Assert.True((await Service().ClaimAsync(draft, ManagerUser)).NotFound); // managers can't see unsubmitted drafts
            AssertError(await Service().ClaimAsync(returned, ManagerUser), "Only submitted");
            Assert.True((await Service().ClaimAsync(9999, ManagerUser)).NotFound);
        }

        [Fact]
        public async Task Claim_WhenAnotherManagerClaimsAtTheSameTime_OnlyOneWins()
        {
            var id = await SubmittedAsync();

            var result = await Service(new ChangeStatusBeforeSave(id, ApplicationStatus.UnderReview, OtherManagerUser.Id))
                .ClaimAsync(id, ManagerUser);

            AssertError(result, "changed by someone else");
            Assert.True(result.Conflict);
            var application = await LoadAsync(id);
            Assert.Equal(OtherManagerUser.Id, application.ReviewerUser);
            Assert.DoesNotContain(application.ApplicationStatusHistories, h => h.NewStatus == (long)ApplicationStatus.UnderReview);
        }

        [Fact]
        public async Task Review_ClaimedByAnotherManager_IsRejected()
        {
            var id = await ClaimedAsync(manager: OtherManagerUser);

            AssertError(await Service().ReviewAsync(id, Review(ReviewOutcome.Approve), ManagerUser), "claimed by another");

            Assert.Equal((long)ApplicationStatus.UnderReview, (await LoadAsync(id)).Status);
            Assert.Equal(0, await _db.CreateContext().Leases.CountAsync());
        }

        [Fact]
        public async Task Review_WhenReleasedAndReclaimedByAnotherManagerMeanwhile_CreatesNoLease()
        {
            // Still Under Review either way, so only the reviewer concurrency token can tell the claims apart.
            var id = await ClaimedAsync(manager: ManagerUser);

            var result = await Service(new ChangeStatusBeforeSave(id, ApplicationStatus.UnderReview, OtherManagerUser.Id))
                .ReviewAsync(id, Review(ReviewOutcome.Approve), ManagerUser);

            AssertError(result, "changed by someone else");
            Assert.True(result.Conflict);
            // (The faked claim ran inside the review's transaction, so it was rolled back with it - only the lease matters here.)
            Assert.Equal(0, await _db.CreateContext().Leases.CountAsync());
        }

        [Fact]
        public async Task Release_PutsItBackInTheQueueWithoutADecision()
        {
            var id = await ClaimedAsync();
            var submittedAt = (await LoadAsync(id)).Submitted;

            AssertOk(await Service().ReleaseAsync(id, ManagerUser));

            var application = await LoadAsync(id);
            Assert.Equal((long)ApplicationStatus.Submitted, application.Status);
            Assert.Null(application.ReviewerUser);
            Assert.Null(application.ReviewClaimed);
            Assert.Equal(submittedAt, application.Submitted); // keeps its place in the queue
            Assert.Contains(application.ApplicationStatusHistories, h => h.PreviousStatus == (long)ApplicationStatus.UnderReview
                && h.NewStatus == (long)ApplicationStatus.Submitted && h.ChangedByUser == ManagerUser.Id && h.Outcome == null);
            AssertOk(await Service().ClaimAsync(id, OtherManagerUser)); // and anyone can claim it again
        }

        [Fact]
        public async Task Release_AnotherManagersClaim_Works()
        {
            var id = await ClaimedAsync(manager: OtherManagerUser);

            AssertOk(await Service().ReleaseAsync(id, ManagerUser));

            var application = await LoadAsync(id);
            Assert.Equal((long)ApplicationStatus.Submitted, application.Status);
            Assert.Equal(ManagerUser.Id, application.ApplicationStatusHistories.OrderBy(h => h.Id).Last().ChangedByUser);
        }

        [Fact]
        public async Task Release_OnlyByManagersAndOnlyUnderReview()
        {
            var waiting = await SubmittedAsync(unitId: _db.SecondUnitId);
            var claimed = await ClaimedAsync(OtherApplicantUser, _db.UnitId);

            AssertError(await Service().ReleaseAsync(waiting, ManagerUser), "Only applications under review");
            Assert.True((await Service().ReleaseAsync(claimed, OtherApplicantUser)).Forbidden);
            Assert.True(await Service().CanReleaseAsync(claimed, OtherManagerUser));
            Assert.False(await Service().CanReleaseAsync(waiting, ManagerUser));
            Assert.False(await Service().CanReleaseAsync(claimed, OtherApplicantUser));
            Assert.Equal((long)ApplicationStatus.UnderReview, (await LoadAsync(claimed)).Status);
        }

        [Fact]
        public async Task Withdraw_UnderReview_SucceedsAndClearsTheClaim()
        {
            var id = await ClaimedAsync();

            AssertOk(await Service().WithdrawAsync(id, ApplicantUser));

            var application = await LoadAsync(id);
            Assert.Equal((long)ApplicationStatus.Withdrawn, application.Status);
            Assert.Null(application.ReviewerUser);
            Assert.Null(application.ReviewClaimed);
        }

        [Fact]
        public async Task Start_WhileUnderReview_ReopensTheSameApplication()
        {
            var id = await ClaimedAsync();

            Assert.Equal(id, await StartAsync());
        }

        [Fact]
        public async Task Editor_ShowsTheClaimToManagersOnly()
        {
            var id = await ClaimedAsync(manager: ManagerUser);

            var mine = await Service().GetEditorAsync(id, null, ManagerUser);
            Assert.Equal("manager-1@example.com", mine!.Reviewer);
            Assert.NotNull(mine.ReviewClaimed);
            Assert.True(mine.ClaimedByMe);
            Assert.True(mine.CanReview);
            Assert.True(mine.CanRelease);
            Assert.False(mine.CanClaim);

            var other = await Service().GetEditorAsync(id, null, OtherManagerUser);
            Assert.Equal("manager-1@example.com", other!.Reviewer);
            Assert.False(other.ClaimedByMe);
            Assert.False(other.CanReview);
            Assert.True(other.CanRelease);

            // The applicant sees the status, but not who's reviewing it.
            var applicant = await Service().GetEditorAsync(id, null, ApplicantUser);
            Assert.Equal(ApplicationStatus.UnderReview, applicant!.Status);
            Assert.Null(applicant.Reviewer);
            Assert.Null(applicant.ReviewClaimed);
            Assert.False(applicant.ClaimedByMe);
            Assert.True(applicant.CanWithdraw);
            Assert.False(applicant.CanEdit);
            Assert.DoesNotContain("manager-1", System.Text.Json.JsonSerializer.Serialize(new object?[]
            {
                applicant,
                await Service().ListAsync(null, null, ApplicantUser)
            }));
        }

        [Fact]
        public async Task Queue_SplitsMineWaitingAndOthersOldestFirst()
        {
            var olderWaiting = await SubmittedAsync(ApplicantUser, _db.UnitId);
            var newerWaiting = await SubmittedAsync(ApplicantUser, _db.SecondUnitId);
            var mine = await ClaimedAsync(OtherApplicantUser, _db.UnitId, ManagerUser);
            var theirs = await ClaimedAsync(OtherApplicantUser, _db.SecondUnitId, OtherManagerUser);
            var decided = await ClaimedAsync(ThirdApplicantUser, _db.UnitId);
            AssertOk(await Service().ReviewAsync(decided, Review(ReviewOutcome.Deny, "No."), ManagerUser));

            var queue = await Service().GetQueueAsync(ManagerUser);

            Assert.Equal(new[] { olderWaiting, newerWaiting }, queue.Waiting.Select(i => i.Id));
            Assert.Equal(new[] { mine }, queue.Mine.Select(i => i.Id));
            var other = Assert.Single(queue.ClaimedByOthers);
            Assert.Equal(theirs, other.Id);
            Assert.Equal("manager-2@example.com", other.Reviewer);
            Assert.NotNull(other.ClaimedAt);
            Assert.Null(queue.Waiting[0].Reviewer);

            // The other manager sees the same applications from their side.
            var theirQueue = await Service().GetQueueAsync(OtherManagerUser);
            Assert.Equal(new[] { theirs }, theirQueue.Mine.Select(i => i.Id));
            Assert.Equal(new[] { mine }, theirQueue.ClaimedByOthers.Select(i => i.Id));
        }

        [Fact]
        public async Task Queue_IsEmptyForApplicants()
        {
            await ClaimedAsync();
            await SubmittedAsync(OtherApplicantUser);

            var queue = await Service().GetQueueAsync(ApplicantUser);

            Assert.Empty(queue.Mine);
            Assert.Empty(queue.Waiting);
            Assert.Empty(queue.ClaimedByOthers);
        }

        // ---------------- Manager notes ----------------

        private const string SecretNote = "PRIVATE-MANAGER-NOTE-7f3a";

        private static ManagerNotesViewModel Notes(string? notes, Guid? version = null)
        {
            return new() { Notes = notes, Version = version };
        }

        [Fact]
        public async Task ManagerNotes_ManagerCanSaveAndReadThem()
        {
            var id = await SubmittedAsync();
            Assert.Null((await Service().GetManagerNotesAsync(id, ManagerUser))!.Notes);

            AssertOk(await Service().SaveManagerNotesAsync(id, Notes("  Called the landlord.\nGood reference.  "), ManagerUser));

            var notes = await Service().GetManagerNotesAsync(id, ManagerUser);
            Assert.NotNull(notes);
            Assert.Equal("Called the landlord.\nGood reference.", notes.Notes);
            Assert.Equal($"{ManagerUser.Id}@example.com", notes.UpdatedBy);
            Assert.NotNull(notes.Version);
            Assert.NotNull(notes.UpdatedAt);
        }

        [Fact]
        public async Task ManagerNotes_SecondSaveWithCurrentVersion_Works()
        {
            var id = await SubmittedAsync();
            AssertOk(await Service().SaveManagerNotesAsync(id, Notes("First."), ManagerUser));
            var version = (await Service().GetManagerNotesAsync(id, ManagerUser))!.Version;

            AssertOk(await Service().SaveManagerNotesAsync(id, Notes("Second.", version), ManagerUser));

            Assert.Equal("Second.", (await Service().GetManagerNotesAsync(id, ManagerUser))!.Notes);
        }

        [Fact]
        public async Task ManagerNotes_ApplicantCanNeitherReadNorWriteThem()
        {
            var id = await SubmittedAsync();
            AssertOk(await Service().SaveManagerNotesAsync(id, Notes(SecretNote), ManagerUser));

            // Even on their own application.
            Assert.Null(await Service().GetManagerNotesAsync(id, ApplicantUser));
            var result = await Service().SaveManagerNotesAsync(id, Notes("Overwritten"), ApplicantUser);
            Assert.True(result.Forbidden);

            Assert.Equal(SecretNote, (await _db.CreateContext().ManagerNotes.SingleAsync()).Notes);
        }

        [Fact]
        public async Task ManagerNotes_NeverAppearInAnythingReturnedToTheApplicant()
        {
            var id = await ClaimedAsync();
            AssertOk(await Service().SaveManagerNotesAsync(id, Notes(SecretNote), ManagerUser));
            AssertOk(await Service().ReviewAsync(id, Review(ReviewOutcome.Return, "Please fix your address."), ManagerUser));

            // Everything the applicant-facing reads hand back, serialized so a note hiding in any property would show.
            object?[] applicantData =
            [
                await Service().GetEditorAsync(id, null, ApplicantUser),
                await Service().GetEditorAsync(id, ApplicationSection.Summary, ApplicantUser),
                await Service().ListAsync(null, null, ApplicantUser),
                await Service().GetHistoryAsync(id, ApplicantUser)
            ];
            Assert.DoesNotContain(SecretNote, System.Text.Json.JsonSerializer.Serialize(applicantData));
        }

        [Fact]
        public void ManagerNotes_CantBeReachedFromARentalApplication()
        {
            // No navigation from RentalApplication to ManagerNote, so no Include or projection over applications can
            // pull the notes in by accident.
            var application = _db.CreateContext().Model.FindEntityType(typeof(RentalApplication))!;
            Assert.DoesNotContain(application.GetNavigations(), n => n.TargetEntityType.ClrType == typeof(ManagerNote));
        }

        [Fact]
        public async Task ManagerNotes_StaleVersion_IsRejectedAndKeepsTheOtherManagersNotes()
        {
            var id = await SubmittedAsync();
            AssertOk(await Service().SaveManagerNotesAsync(id, Notes("First."), ManagerUser));
            var opened = (await Service().GetManagerNotesAsync(id, ManagerUser))!.Version;
            AssertOk(await Service().SaveManagerNotesAsync(id, Notes("Another manager's edit.", opened), ManagerUser));

            var result = await Service().SaveManagerNotesAsync(id, Notes("Mine.", opened), ManagerUser);

            AssertError(result, "changed by someone else");
            Assert.True(result.Conflict);
            Assert.Equal("Another manager's edit.", (await Service().GetManagerNotesAsync(id, ManagerUser))!.Notes);
        }

        [Fact]
        public async Task ManagerNotes_FormOpenedBeforeFirstNote_IsRejectedOnceSomeoneAddedOne()
        {
            var id = await SubmittedAsync();
            AssertOk(await Service().SaveManagerNotesAsync(id, Notes("Another manager got here first."), ManagerUser));

            // This form was opened when there were no notes, so it has no version.
            var result = await Service().SaveManagerNotesAsync(id, Notes("Mine."), ManagerUser);

            Assert.True(result.Conflict);
            Assert.Equal("Another manager got here first.", (await Service().GetManagerNotesAsync(id, ManagerUser))!.Notes);
        }

        [Fact]
        public async Task ManagerNotes_StayEditableAfterADecision()
        {
            var id = await ClaimedAsync();
            AssertOk(await Service().ReviewAsync(id, Review(ReviewOutcome.Approve), ManagerUser));

            AssertOk(await Service().SaveManagerNotesAsync(id, Notes("Lease signed."), ManagerUser));
        }

        [Fact]
        public async Task ManagerNotes_NotAvailableOnUnsubmittedDraftsOrMissingApplications()
        {
            var draft = await StartAsync();

            Assert.Null(await Service().GetManagerNotesAsync(draft, ManagerUser));
            Assert.True((await Service().SaveManagerNotesAsync(draft, Notes("x"), ManagerUser)).NotFound);
            Assert.True((await Service().SaveManagerNotesAsync(9999, Notes("x"), ManagerUser)).NotFound);
            Assert.Empty(_db.CreateContext().ManagerNotes);
        }

        // ---------------- List and history ----------------

        [Fact]
        public async Task List_ApplicantSeesOnlyOwnApplications()
        {
            var mine = await SubmittedAsync(ApplicantUser);
            var theirs = await SubmittedAsync(OtherApplicantUser);

            var applicantList = await Service().ListAsync(null, null, ApplicantUser);
            var managerList = await Service().ListAsync(null, null, ManagerUser);

            Assert.Equal(new[] { mine }, applicantList.Select(a => a.Id));
            Assert.Equal(new[] { theirs, mine }, managerList.Select(a => a.Id)); // newest first
        }

        [Fact]
        public async Task NeverSubmittedDraft_IsHiddenFromManagers()
        {
            var draft = await StartAsync(ApplicantUser);

            Assert.Empty(await Service().ListAsync(null, null, ManagerUser));
            Assert.Null(await Service().GetEditorAsync(draft, null, ManagerUser));
            Assert.Equal(new[] { draft }, (await Service().ListAsync(null, null, ApplicantUser)).Select(a => a.Id));
        }

        [Fact]
        public async Task List_FiltersByStatusAndProperty()
        {
            var draft = await StartAsync(unitId: _db.SecondUnitId);
            var submitted = await SubmittedAsync(unitId: _db.UnitId);

            Assert.Equal(new[] { submitted }, (await Service().ListAsync(ApplicationStatus.Submitted, null, ManagerUser)).Select(a => a.Id));
            Assert.Equal(new[] { draft }, (await Service().ListAsync(ApplicationStatus.Draft, null, ApplicantUser)).Select(a => a.Id));
            Assert.Equal(2, (await Service().ListAsync(null, _db.PropertyId, ApplicantUser)).Count);
            Assert.Empty(await Service().ListAsync(null, 9999, ApplicantUser));
        }

        [Fact]
        public async Task History_RecordsEachChangeAndIsManagerOnly()
        {
            var id = await ClaimedAsync();
            AssertOk(await Service().ReviewAsync(id, Review(ReviewOutcome.Deny, "Income too low."), ManagerUser));

            var history = await Service().GetHistoryAsync(id, ManagerUser);

            (ApplicationStatus?, ApplicationStatus)[] expected =
            [
                (null, ApplicationStatus.Draft),
                (ApplicationStatus.Draft, ApplicationStatus.Submitted),
                (ApplicationStatus.Submitted, ApplicationStatus.UnderReview),
                (ApplicationStatus.UnderReview, ApplicationStatus.Denied)
            ];
            Assert.Equal(expected, history.Select(h => (h.FromStatus, h.ToStatus)));
            Assert.Equal("manager-1@example.com", history[^1].ChangedBy);
            Assert.Equal("Income too low.", history[^1].Comment);
            Assert.Empty(await Service().GetHistoryAsync(id, ApplicantUser));
        }
    }
}
