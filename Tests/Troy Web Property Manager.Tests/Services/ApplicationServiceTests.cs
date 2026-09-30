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
            AssertOk(await Service().SaveApplicantInformationAsync(id, Info(), user));
            AssertOk(await Service().SaveResidenceAsync(id, Residence(), user));
            AssertOk(await Service().SaveResidenceHistoryAsync(id, user));
            return id;
        }

        private async Task<int> SubmittedAsync(CurrentUser? user = null, int? unitId = null)
        {
            user ??= ApplicantUser;
            var id = await CompleteDraftAsync(user, unitId);
            AssertOk(await Service().SubmitAsync(id, user));
            return id;
        }

        /// <summary>Gives the unit an active lease by approving another applicant's application for it.</summary>
        private async Task LeaseUnitAsync(int unitId)
        {
            var id = await SubmittedAsync(OtherApplicantUser, unitId);
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

        /// <summary>Fakes another request changing the status after the service loaded the application but before it saves.</summary>
        private sealed class ChangeStatusBeforeSave(int applicationId, ApplicationStatus status) : SaveChangesInterceptor
        {
            public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                await eventData.Context!.Database.ExecuteSqlAsync(
                    $"UPDATE RentalApplications SET Status = {(long)status} WHERE id = {applicationId}", cancellationToken);
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

            AssertOk(await Service().SaveApplicantInformationAsync(draft, Info(), ApplicantUser));
            AssertOk(await Service().SaveResidenceAsync(draft, Residence(), ApplicantUser));
            AssertOk(await Service().SaveResidenceHistoryAsync(draft, ApplicantUser));
            AssertOk(await Service().SubmitAsync(draft, ApplicantUser));
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
            Assert.True(editor.CanReview);
            Assert.False(editor.CanWithdraw);
        }

        [Fact]
        public async Task GetEditor_NewApplication_IsPrefilledFromLastSavedDetails()
        {
            var first = await StartAsync(unitId: _db.UnitId);
            AssertOk(await Service().SaveApplicantInformationAsync(first, Info("first@example.com"), ApplicantUser));

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
            AssertOk(await Service().SaveApplicantInformationAsync(draft, Info("changed@example.com"), ApplicantUser));

            // The submitted application keeps exactly what was submitted.
            Assert.Equal("alex@example.com", (await LoadAsync(submitted)).ApplicantInformation!.Email);
            Assert.Equal("changed@example.com", (await LoadAsync(draft)).ApplicantInformation!.Email);
        }

        // ---------------- Sections and submit ----------------

        [Fact]
        public async Task SaveResidenceHistory_WithoutResidences_IsRejected()
        {
            var id = await StartAsync();
            AssertError(await Service().SaveResidenceHistoryAsync(id, ApplicantUser), "at least one prior residence");
        }

        [Fact]
        public async Task SaveResidence_OnOtherApplicantsApplication_IsNotFound()
        {
            var id = await StartAsync();
            Assert.True((await Service().SaveResidenceAsync(id, Residence(), OtherApplicantUser)).NotFound);
        }

        [Fact]
        public async Task DeleteLastResidence_MarksSectionUnsaved()
        {
            var id = await CompleteDraftAsync();
            var residenceId = (await Service().GetEditorAsync(id, null, ApplicantUser))!.Residences.Single().ResidenceId!.Value;

            AssertOk(await Service().DeleteResidenceAsync(id, residenceId, ApplicantUser));

            Assert.False((await LoadAsync(id)).ResidenceHistorySaved);
            AssertError(await Service().SubmitAsync(id, ApplicantUser), SubmissionRules.ResidenceHistoryNotSaved);
        }

        [Fact]
        public async Task Submit_BeforeBothSectionsSaved_IsRejected()
        {
            var id = await StartAsync();
            AssertOk(await Service().SaveApplicantInformationAsync(id, Info(), ApplicantUser));

            AssertError(await Service().SubmitAsync(id, ApplicantUser), SubmissionRules.ResidenceHistoryNotSaved);
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

            AssertError(await Service().SubmitAsync(id, ApplicantUser), SubmissionRules.UnitLeased);
            // The Summary shows the same reason and keeps Submit disabled.
            var editor = (await Service().GetEditorAsync(id, ApplicationSection.Summary, ApplicantUser))!;
            Assert.Equal([SubmissionRules.UnitLeased], editor.SubmitBlockers);
            Assert.False(editor.CanSubmit);
        }

        [Fact]
        public async Task Edits_AfterSubmit_AreRejected()
        {
            var id = await SubmittedAsync();

            AssertError(await Service().SaveApplicantInformationAsync(id, Info(), ApplicantUser), "can no longer be edited");
            AssertError(await Service().SaveResidenceAsync(id, Residence(), ApplicantUser), "can no longer be edited");
            AssertError(await Service().SubmitAsync(id, ApplicantUser), "can no longer be edited");
        }

        [Fact]
        public async Task SaveResidence_WhenSubmittedConcurrently_IsRejected()
        {
            var id = await CompleteDraftAsync();

            var result = await Service(new ChangeStatusBeforeSave(id, ApplicationStatus.Submitted)).SaveResidenceAsync(id, Residence(), ApplicantUser);

            Assert.True(result.Conflict);
            Assert.Single(_db.CreateContext().Residences.Where(r => r.RentalApplicationId == id));
        }

        [Fact]
        public async Task SaveApplicantInformation_AlreadySavedAndSubmittedConcurrently_IsRejected()
        {
            // Section 1 is already saved, so this save doesn't change the application row itself.
            var id = await CompleteDraftAsync();

            var result = await Service(new ChangeStatusBeforeSave(id, ApplicationStatus.Submitted))
                .SaveApplicantInformationAsync(id, Info("changed@example.com"), ApplicantUser);

            Assert.True(result.Conflict);
            Assert.NotEqual("changed@example.com", _db.CreateContext().ApplicantInformation.Single(i => i.RentalApplicationId == id).Email);
        }

        [Fact]
        public async Task DeleteResidence_WhenSubmittedConcurrently_IsRejected()
        {
            var id = await CompleteDraftAsync();
            AssertOk(await Service().SaveResidenceAsync(id, Residence(), ApplicantUser));
            var residenceId = (await Service().GetEditorAsync(id, null, ApplicantUser))!.Residences.First().ResidenceId!.Value;

            var result = await Service(new ChangeStatusBeforeSave(id, ApplicationStatus.Submitted)).DeleteResidenceAsync(id, residenceId, ApplicantUser);

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
            var id = await SubmittedAsync();
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
        public async Task CanReview_OnlyManagerAndOnlySubmitted()
        {
            var draft = await StartAsync(unitId: _db.SecondUnitId);
            var submitted = await SubmittedAsync(unitId: _db.UnitId);

            Assert.True(await Service().CanReviewAsync(submitted, ManagerUser));
            Assert.False(await Service().CanReviewAsync(submitted, ApplicantUser));
            Assert.False(await Service().CanReviewAsync(draft, ManagerUser));
            Assert.False(await Service().CanReviewAsync(9999, ManagerUser));
        }

        [Fact]
        public async Task Review_Approve_CreatesTwelveMonthLease()
        {
            var id = await SubmittedAsync();

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
            var id = await SubmittedAsync();
            AssertOk(await Service().ReviewAsync(id, Review(ReviewOutcome.Approve), ManagerUser));

            var available = await new PropertyService(_db.CreateContext()).GetAvailableUnitsAsync();
            Assert.Equal(new[] { _db.SecondUnitId }, available.Select(u => u.Id));
        }

        [Fact]
        public async Task Review_Return_ShowsCommentToApplicantAndReopensEditing()
        {
            var id = await SubmittedAsync();

            AssertOk(await Service().ReviewAsync(id, Review(ReviewOutcome.Return, " Add a second reference. "), ManagerUser));

            var editor = await Service().GetEditorAsync(id, null, ApplicantUser);
            Assert.Equal(ApplicationStatus.Returned, editor!.Status);
            Assert.Equal("Add a second reference.", editor.ReviewComment);
            Assert.True(editor.CanEdit);
            Assert.True(editor.CanSubmit); // both sections are still saved, so it can be resubmitted as is
            AssertOk(await Service().SubmitAsync(id, ApplicantUser));
        }

        [Fact]
        public async Task Review_Deny_ShowsCommentToApplicant()
        {
            var id = await SubmittedAsync();
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
            var id = await SubmittedAsync();

            var result = await Service().ReviewAsync(id, Review(outcome, "  "), ManagerUser);

            AssertError(result, "comment is required");
            Assert.True(result.Errors.ContainsKey(nameof(ReviewViewModel.Comment)));
            Assert.Equal((long)ApplicationStatus.Submitted, (await LoadAsync(id)).Status);
        }

        [Fact]
        public async Task Review_ByApplicant_IsRejected()
        {
            var id = await SubmittedAsync();
            var result = await Service().ReviewAsync(id, Review(ReviewOutcome.Approve), ApplicantUser);
            AssertError(result, "Only property managers");
            Assert.True(result.Forbidden);
        }

        [Fact]
        public async Task Review_Draft_IsRejected()
        {
            var id = await CompleteDraftAsync();
            AssertError(await Service().ReviewAsync(id, Review(ReviewOutcome.Approve), ManagerUser), "Only submitted");
        }

        [Fact]
        public async Task Review_ApproveSecondApplicationForSameUnit_IsRejected()
        {
            var first = await SubmittedAsync(ApplicantUser);
            var second = await SubmittedAsync(OtherApplicantUser);
            AssertOk(await Service().ReviewAsync(first, Review(ReviewOutcome.Approve), ManagerUser));

            AssertError(await Service().ReviewAsync(second, Review(ReviewOutcome.Approve), ManagerUser), "already has an active lease");

            Assert.Equal(1, await _db.CreateContext().Leases.CountAsync());
            Assert.Equal((long)ApplicationStatus.Submitted, (await LoadAsync(second)).Status);
        }

        [Fact]
        public async Task Review_WhenWithdrawnConcurrently_CreatesNoLease()
        {
            var id = await SubmittedAsync();

            var result = await Service(new ChangeStatusBeforeSave(id, ApplicationStatus.Withdrawn))
                .ReviewAsync(id, Review(ReviewOutcome.Approve), ManagerUser);

            AssertError(result, "changed by someone else");
            Assert.True(result.Conflict);
            Assert.Equal(0, await _db.CreateContext().Leases.CountAsync());
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
            var id = await SubmittedAsync();
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
            var id = await SubmittedAsync();
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
            var id = await SubmittedAsync();
            AssertOk(await Service().ReviewAsync(id, Review(ReviewOutcome.Deny, "Income too low."), ManagerUser));

            var history = await Service().GetHistoryAsync(id, ManagerUser);

            (ApplicationStatus?, ApplicationStatus)[] expected =
            [
                (null, ApplicationStatus.Draft),
                (ApplicationStatus.Draft, ApplicationStatus.Submitted),
                (ApplicationStatus.Submitted, ApplicationStatus.Denied)
            ];
            Assert.Equal(expected, history.Select(h => (h.FromStatus, h.ToStatus)));
            Assert.Equal("manager-1@example.com", history[^1].ChangedBy);
            Assert.Equal("Income too low.", history[^1].Comment);
            Assert.Empty(await Service().GetHistoryAsync(id, ApplicantUser));
        }
    }
}
