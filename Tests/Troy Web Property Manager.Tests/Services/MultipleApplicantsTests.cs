using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;
using Troy_Web_Property_Manager.ViewModels;
using static Troy_Web_Property_Manager.Tests.TestDatabase;

namespace Troy_Web_Property_Manager.Tests.Services
{
    /// <summary>
    /// Several applicants on one application: who can see and edit it, adding and removing applicants, and two of them
    /// saving at the same time. <see cref="ApplicantUser"/> starts the applications here and adds
    /// <see cref="OtherApplicantUser"/>; <see cref="ThirdApplicantUser"/> is never on them.
    /// </summary>
    public sealed class MultipleApplicantsTests : IDisposable
    {
        private readonly TestDatabase _db = new();

        public void Dispose()
        {
            _db.Dispose();
        }

        // ---------------- Helpers ----------------

        private ApplicationService Service(params IInterceptor[] interceptors)
        {
            return new(_db.CreateContext(interceptors));
        }

        private static void AssertOk(ServiceResult result)
        {
            Assert.True(result.Succeeded, $"NotFound={result.NotFound}; {string.Join("; ", result.Errors.Values)}");
        }

        private static void AssertStale(ServiceResult result, string message = ApplicationService.SectionChangedMessage)
        {
            Assert.False(result.Succeeded);
            Assert.True(result.Conflict);
            Assert.Equal(message, result.Errors[""]);
        }

        private static ApplicantInformationViewModel Info(string name = "Alex Applicant")
        {
            return new() { Name = name, Phone = "518-555-0100", Email = "alex@example.com", CurrentAddress = "9 Oak Ave" };
        }

        private static ResidenceViewModel Residence(Guid version, string address = "5 Elm St", int? residenceId = null)
        {
            return new()
            {
                ResidenceId = residenceId,
                SectionVersion = version,
                Address = address,
                LandlordName = "Pat Landlord",
                LandlordPhone = "518-555-0199",
                MoveInDate = new DateOnly(2020, 1, 1),
                MoveOutDate = new DateOnly(2024, 12, 31)
            };
        }

        private static string Email(CurrentUser user)
        {
            return $"{user.Id}@example.com";
        }

        /// <summary>What a freshly loaded page would carry: the current version of each section.</summary>
        private async Task<(Guid Info, Guid History)> VersionsAsync(int id)
        {
            var editor = await Service().GetEditorAsync(id, null, ApplicantUser);
            return (editor!.ApplicantInformationVersion, editor.ResidenceHistoryVersion);
        }

        private async Task<int> StartAsync(CurrentUser? user = null, int? unitId = null)
        {
            var result = await Service().StartAsync(unitId ?? _db.UnitId, user ?? ApplicantUser);
            AssertOk(result);
            return result.Id;
        }

        /// <summary>A draft started by <see cref="ApplicantUser"/> with <see cref="OtherApplicantUser"/> added to it.</summary>
        private async Task<int> SharedDraftAsync(int? unitId = null)
        {
            var id = await StartAsync(ApplicantUser, unitId);
            AssertOk(await Service().AddApplicantAsync(id, new AddApplicantViewModel { Email = Email(OtherApplicantUser) }, ApplicantUser));
            return id;
        }

        /// <summary>Both sections saved and valid, ready to submit.</summary>
        private async Task CompleteAsync(int id, CurrentUser user)
        {
            var (info, history) = await VersionsAsync(id);
            AssertOk(await Service().SaveApplicantInformationAsync(id, Info(), info, user));
            var saved = await Service().SaveResidenceAsync(id, Residence(history), user);
            AssertOk(saved);
            AssertOk(await Service().SaveResidenceHistoryAsync(id, saved.Version!.Value, user));
        }

        private int ApplicantIdOf(CurrentUser user)
        {
            return _db.CreateContext().Applicants.Single(a => a.UserId == user.Id).Id;
        }

        // ---------------- Who's on it ----------------

        [Fact]
        public async Task Start_PutsTheStarterOnIt()
        {
            var id = await StartAsync();

            var membership = Assert.Single(_db.CreateContext().ApplicationApplicants.Where(m => m.RentalApplicationId == id));
            Assert.Equal(ApplicantIdOf(ApplicantUser), membership.ApplicantId);
            Assert.Equal(ApplicantUser.Id, membership.AddedByUser);
        }

        [Fact]
        public async Task AddedApplicant_CanViewEditAndSubmit()
        {
            var id = await SharedDraftAsync();

            var editor = await Service().GetEditorAsync(id, null, OtherApplicantUser);
            Assert.NotNull(editor);
            Assert.True(editor.CanEdit);
            Assert.Equal(new[] { id }, (await Service().ListAsync(new(), OtherApplicantUser)).Items.Select(a => a.Id));

            await CompleteAsync(id, OtherApplicantUser);
            var (info, history) = await VersionsAsync(id);
            AssertOk(await Service().SubmitAsync(id, info, history, OtherApplicantUser));

            Assert.Equal((long)ApplicationStatus.Submitted, (await _db.CreateContext().RentalApplications.SingleAsync(a => a.Id == id)).Status);
            // Both applicants still see it, now read-only.
            Assert.False((await Service().GetEditorAsync(id, null, ApplicantUser))!.CanEdit);
            Assert.False((await Service().GetEditorAsync(id, null, OtherApplicantUser))!.CanEdit);
        }

        [Fact]
        public async Task AddedApplicant_CanWithdraw()
        {
            var id = await SharedDraftAsync();

            AssertOk(await Service().WithdrawAsync(id, OtherApplicantUser));

            Assert.Equal((long)ApplicationStatus.Withdrawn, (await _db.CreateContext().RentalApplications.SingleAsync(a => a.Id == id)).Status);
        }

        [Fact]
        public async Task ApplicantNotOnIt_StillCantSeeOrTouchIt()
        {
            var id = await SharedDraftAsync();
            var (info, history) = await VersionsAsync(id);

            Assert.Null(await Service().GetEditorAsync(id, null, ThirdApplicantUser));
            Assert.Empty((await Service().ListAsync(new(), ThirdApplicantUser)).Items);
            Assert.True((await Service().SaveApplicantInformationAsync(id, Info(), info, ThirdApplicantUser)).NotFound);
            Assert.True((await Service().SaveResidenceAsync(id, Residence(history), ThirdApplicantUser)).NotFound);
            Assert.True((await Service().WithdrawAsync(id, ThirdApplicantUser)).NotFound);
            Assert.True((await Service().AddApplicantAsync(id, new AddApplicantViewModel { Email = Email(ThirdApplicantUser) }, ThirdApplicantUser)).NotFound);
        }

        [Fact]
        public async Task Editor_ListsTheApplicants_StarterFirst()
        {
            var id = await SharedDraftAsync();

            var applicants = (await Service().GetEditorAsync(id, null, OtherApplicantUser))!.Applicants;

            Assert.Equal(new[] { Email(ApplicantUser), Email(OtherApplicantUser) }, applicants.Select(a => a.Email));
            Assert.Equal(new[] { true, false }, applicants.Select(a => a.IsStarter));
            Assert.Equal(new[] { false, true }, applicants.Select(a => a.IsYou));
        }

        [Fact]
        public async Task Start_ForTheSameUnit_ReopensTheSharedApplication()
        {
            var id = await SharedDraftAsync();

            Assert.Equal(id, await StartAsync(OtherApplicantUser));
        }

        // ---------------- Adding and removing ----------------

        [Theory]
        [InlineData("nobody@example.com", "no applicant account")]
        [InlineData("manager-1@example.com", "no applicant account")] // managers can't be added
        [InlineData("applicant-1@example.com", "already on this application")] // the starter
        public async Task Add_IsRejected_WithTheReasonUnderEmail(string email, string reason)
        {
            var id = await StartAsync();

            var result = await Service().AddApplicantAsync(id, new AddApplicantViewModel { Email = email }, ApplicantUser);

            Assert.False(result.Succeeded);
            Assert.Contains(reason, result.Errors[nameof(AddApplicantViewModel.Email)]);
            Assert.Single(_db.CreateContext().ApplicationApplicants.Where(m => m.RentalApplicationId == id));
        }

        [Fact]
        public async Task Add_MatchesTheEmailWhateverTheCase()
        {
            var id = await StartAsync();

            AssertOk(await Service().AddApplicantAsync(id, new AddApplicantViewModel { Email = "  Applicant-2@Example.COM " }, ApplicantUser));

            Assert.NotNull(await Service().GetEditorAsync(id, null, OtherApplicantUser));
        }

        [Fact]
        public async Task Add_SomeoneWithTheirOwnOpenApplicationForTheUnit_IsRejected()
        {
            await StartAsync(OtherApplicantUser, _db.UnitId);
            var id = await StartAsync(ApplicantUser, _db.UnitId);

            var result = await Service().AddApplicantAsync(id, new AddApplicantViewModel { Email = Email(OtherApplicantUser) }, ApplicantUser);

            Assert.Contains("already have an open application for this unit", result.Errors[nameof(AddApplicantViewModel.Email)]);
        }

        [Fact]
        public async Task Add_SomeoneAlreadyAddedToAnotherOpenApplicationForTheUnit_IsRejected()
        {
            // OtherApplicantUser didn't start an application for the unit, but is on ApplicantUser's.
            await SharedDraftAsync(_db.UnitId);
            var id = await StartAsync(ThirdApplicantUser, _db.UnitId);

            var result = await Service().AddApplicantAsync(id, new AddApplicantViewModel { Email = Email(OtherApplicantUser) }, ThirdApplicantUser);

            Assert.Contains("already have an open application for this unit", result.Errors[nameof(AddApplicantViewModel.Email)]);
            Assert.Single(_db.CreateContext().ApplicationApplicants.Where(m => m.RentalApplicationId == id));
        }

        [Fact]
        public async Task Add_LosingADeadlock_RetriesAndAddsThem()
        {
            var id = await StartAsync();

            var result = await Service(new FakeSqlErrors.OnSave(FakeSqlErrors.Deadlock))
                .AddApplicantAsync(id, new AddApplicantViewModel { Email = Email(OtherApplicantUser) }, ApplicantUser);

            AssertOk(result);
            Assert.Equal(2, _db.CreateContext().ApplicationApplicants.Count(m => m.RentalApplicationId == id));
        }

        [Fact]
        public async Task Add_LosingAUniqueRace_RetriesAndAddsThem()
        {
            var id = await StartAsync();

            var result = await Service(new FakeSqlErrors.OnSave(FakeSqlErrors.UniqueViolation))
                .AddApplicantAsync(id, new AddApplicantViewModel { Email = Email(OtherApplicantUser) }, ApplicantUser);

            AssertOk(result);
            Assert.Equal(2, _db.CreateContext().ApplicationApplicants.Count(m => m.RentalApplicationId == id));
        }

        [Fact]
        public async Task Add_LosingEveryAttempt_IsStale_AndAddsNoOne()
        {
            var id = await StartAsync();

            var result = await Service(new FakeSqlErrors.OnSave(FakeSqlErrors.Deadlock, FakeSqlErrors.UniqueViolation, FakeSqlErrors.Deadlock))
                .AddApplicantAsync(id, new AddApplicantViewModel { Email = Email(OtherApplicantUser) }, ApplicantUser);

            Assert.True(result.Conflict);
            Assert.Single(_db.CreateContext().ApplicationApplicants.Where(m => m.RentalApplicationId == id));
            Assert.False(_db.CreateContext().Applicants.Any(a => a.UserId == OtherApplicantUser.Id));
        }

        [Fact]
        public async Task Add_CreatesTheirProfileIfTheyNeverApplied()
        {
            var id = await StartAsync();
            Assert.False(_db.CreateContext().Applicants.Any(a => a.UserId == OtherApplicantUser.Id));

            AssertOk(await Service().AddApplicantAsync(id, new AddApplicantViewModel { Email = Email(OtherApplicantUser) }, ApplicantUser));

            var profile = _db.CreateContext().Applicants.Single(a => a.UserId == OtherApplicantUser.Id);
            Assert.Equal(Email(OtherApplicantUser), profile.Email);
        }

        [Fact]
        public async Task Add_OnceSubmitted_IsRejected()
        {
            var id = await SharedDraftAsync();
            await CompleteAsync(id, ApplicantUser);
            var (info, history) = await VersionsAsync(id);
            AssertOk(await Service().SubmitAsync(id, info, history, ApplicantUser));

            var result = await Service().AddApplicantAsync(id, new AddApplicantViewModel { Email = Email(ThirdApplicantUser) }, ApplicantUser);

            Assert.Contains("can no longer be edited", result.Errors[""]);
        }

        [Fact]
        public async Task Remove_TakesAwayTheirAccess()
        {
            var id = await SharedDraftAsync();
            var (info, _) = await VersionsAsync(id);

            AssertOk(await Service().RemoveApplicantAsync(id, ApplicantIdOf(OtherApplicantUser), ApplicantUser));

            Assert.Null(await Service().GetEditorAsync(id, null, OtherApplicantUser));
            // A page they still had open can't save either.
            Assert.True((await Service().SaveApplicantInformationAsync(id, Info(), info, OtherApplicantUser)).NotFound);
        }

        [Fact]
        public async Task Remove_Yourself_LeavesTheApplication()
        {
            var id = await SharedDraftAsync();

            AssertOk(await Service().RemoveApplicantAsync(id, ApplicantIdOf(OtherApplicantUser), OtherApplicantUser));

            Assert.Null(await Service().GetEditorAsync(id, null, OtherApplicantUser));
            Assert.NotNull(await Service().GetEditorAsync(id, null, ApplicantUser));
        }

        [Fact]
        public async Task Remove_TheStarter_IsRejected()
        {
            var id = await SharedDraftAsync();

            var result = await Service().RemoveApplicantAsync(id, ApplicantIdOf(ApplicantUser), OtherApplicantUser);

            Assert.Contains("started this application can't be removed", result.Errors[""]);
            Assert.NotNull(await Service().GetEditorAsync(id, null, ApplicantUser));
        }

        [Fact]
        public async Task OnlyTheStartersSaves_UpdateTheProfileUsedForPrefill()
        {
            var id = await SharedDraftAsync();
            var (info, _) = await VersionsAsync(id);

            AssertOk(await Service().SaveApplicantInformationAsync(id, Info("Saved By Other"), info, OtherApplicantUser));

            var db = _db.CreateContext();
            Assert.NotEqual("Saved By Other", db.Applicants.Single(a => a.UserId == ApplicantUser.Id).Name);
            Assert.NotEqual("Saved By Other", db.Applicants.Single(a => a.UserId == OtherApplicantUser.Id).Name);

            (info, _) = await VersionsAsync(id);
            AssertOk(await Service().SaveApplicantInformationAsync(id, Info("Saved By Starter"), info, ApplicantUser));
            Assert.Equal("Saved By Starter", _db.CreateContext().Applicants.Single(a => a.UserId == ApplicantUser.Id).Name);
        }

        // ---------------- Two applicants saving at once ----------------

        [Fact]
        public async Task DifferentSections_BothSave()
        {
            var id = await SharedDraftAsync();
            // Both applicants loaded the page at the same moment.
            var (info, history) = await VersionsAsync(id);

            AssertOk(await Service().SaveApplicantInformationAsync(id, Info("From A"), info, ApplicantUser));
            AssertOk(await Service().SaveResidenceAsync(id, Residence(history, "From B"), OtherApplicantUser));

            var editor = await Service().GetEditorAsync(id, null, ApplicantUser);
            Assert.Equal("From A", editor!.ApplicantInformation.Name);
            Assert.Equal("From B", Assert.Single(editor.Residences).Address);
        }

        [Fact]
        public async Task DifferentSections_SavedAtTheSameInstant_BothSave()
        {
            var id = await SharedDraftAsync();
            var (info, history) = await VersionsAsync(id);

            // While A's Applicant Information save is in flight, B's Residence History save lands: it swaps the
            // residence version and marks the section saved.
            var otherSave = new SqlBeforeSave(
                $"UPDATE RentalApplications SET ResidenceHistoryVersion = '{Guid.NewGuid().ToString().ToUpperInvariant()}', ResidenceHistorySaved = 1 WHERE id = {id}");
            AssertOk(await Service(otherSave).SaveApplicantInformationAsync(id, Info("From A"), info, ApplicantUser));

            var application = await _db.CreateContext().RentalApplications.Include(a => a.ApplicantInformation).SingleAsync(a => a.Id == id);
            Assert.Equal("From A", application.ApplicantInformation!.Name);
            Assert.True(application.ResidenceHistorySaved); // B's change wasn't overwritten
            Assert.NotEqual(history, application.ResidenceHistoryVersion);
        }

        [Fact]
        public async Task SameSection_ApplicantInformation_SecondSaveIsStale()
        {
            var id = await SharedDraftAsync();
            var (info, _) = await VersionsAsync(id);

            AssertOk(await Service().SaveApplicantInformationAsync(id, Info("From A"), info, ApplicantUser));
            AssertStale(await Service().SaveApplicantInformationAsync(id, Info("From B"), info, OtherApplicantUser));

            Assert.Equal("From A", (await Service().GetEditorAsync(id, null, ApplicantUser))!.ApplicantInformation.Name);
        }

        [Fact]
        public async Task SameSection_Residences_SecondSaveIsStale()
        {
            var id = await SharedDraftAsync();
            var (_, history) = await VersionsAsync(id);
            var first = await Service().SaveResidenceAsync(id, Residence(history, "Original"), ApplicantUser);
            AssertOk(first);
            // Both open the Edit modal for the same residence.
            var opened = first.Version!.Value;

            AssertOk(await Service().SaveResidenceAsync(id, Residence(opened, "From A", first.Id), ApplicantUser));
            AssertStale(await Service().SaveResidenceAsync(id, Residence(opened, "From B", first.Id), OtherApplicantUser));
            // Adding a residence from a stale modal is a save to the same section too.
            AssertStale(await Service().SaveResidenceAsync(id, Residence(opened, "New from B"), OtherApplicantUser));

            Assert.Equal("From A", Assert.Single((await Service().GetEditorAsync(id, null, ApplicantUser))!.Residences).Address);
        }

        [Fact]
        public async Task SameSection_DeleteAfterAnotherSave_IsStale()
        {
            var id = await SharedDraftAsync();
            var (_, history) = await VersionsAsync(id);
            var first = await Service().SaveResidenceAsync(id, Residence(history), ApplicantUser);
            var opened = first.Version!.Value;

            AssertOk(await Service().SaveResidenceAsync(id, Residence(opened, "Edited by A", first.Id), ApplicantUser));
            AssertStale(await Service().DeleteResidenceAsync(id, first.Id, opened, OtherApplicantUser));

            Assert.Single(_db.CreateContext().Residences.Where(r => r.RentalApplicationId == id));
        }

        [Fact]
        public async Task SameSection_ResidenceHistoryContinue_SecondIsStale()
        {
            var id = await SharedDraftAsync();
            var (_, history) = await VersionsAsync(id);

            AssertOk(await Service().SaveResidenceHistoryAsync(id, history, ApplicantUser));
            AssertStale(await Service().SaveResidenceHistoryAsync(id, history, OtherApplicantUser));
        }

        [Fact]
        public async Task OwnConsecutiveSaves_UseTheReturnedVersion()
        {
            var id = await SharedDraftAsync();
            var (_, history) = await VersionsAsync(id);

            var first = await Service().SaveResidenceAsync(id, Residence(history, "One"), ApplicantUser);
            var second = await Service().SaveResidenceAsync(id, Residence(first.Version!.Value, "One, fixed", first.Id), ApplicantUser);

            AssertOk(second);
            Assert.NotEqual(first.Version, second.Version);
        }

        [Fact]
        public async Task StaleSave_ChangesNothing_AndWorksAfterReloading()
        {
            var id = await SharedDraftAsync();
            var (info, _) = await VersionsAsync(id);
            AssertOk(await Service().SaveApplicantInformationAsync(id, Info("From A"), info, ApplicantUser));
            var afterA = (await VersionsAsync(id)).Info;

            AssertStale(await Service().SaveApplicantInformationAsync(id, Info("From B"), info, OtherApplicantUser));
            Assert.Equal(afterA, (await VersionsAsync(id)).Info); // rolled back, version untouched

            // Reload (current version) and save again.
            AssertOk(await Service().SaveApplicantInformationAsync(id, Info("From B"), afterA, OtherApplicantUser));
            Assert.Equal("From B", (await Service().GetEditorAsync(id, null, ApplicantUser))!.ApplicantInformation.Name);
        }

        [Fact]
        public async Task Submit_AfterAnotherApplicantChangedASection_IsStale()
        {
            var id = await SharedDraftAsync();
            await CompleteAsync(id, ApplicantUser);
            // A opens the Summary...
            var (info, history) = await VersionsAsync(id);
            // ...and B changes Applicant Information before A clicks Submit.
            AssertOk(await Service().SaveApplicantInformationAsync(id, Info("Changed by B"), info, OtherApplicantUser));

            AssertStale(await Service().SubmitAsync(id, info, history, ApplicantUser), ApplicationService.ChangedBeforeSubmitMessage);
            Assert.Equal((long)ApplicationStatus.Draft, (await _db.CreateContext().RentalApplications.SingleAsync(a => a.Id == id)).Status);

            var (currentInfo, currentHistory) = await VersionsAsync(id);
            AssertOk(await Service().SubmitAsync(id, currentInfo, currentHistory, ApplicantUser));
        }

        /// <summary>Runs some SQL as if another request's save landed just before this one's.</summary>
        private sealed class SqlBeforeSave(string sql) : SaveChangesInterceptor
        {
            public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                await eventData.Context!.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                return result;
            }
        }
    }
}
