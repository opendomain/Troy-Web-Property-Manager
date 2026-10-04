using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Troy_Web_Property_Manager.Controllers;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;
using Troy_Web_Property_Manager.ViewModels;
using static Troy_Web_Property_Manager.Tests.TestDatabase;

namespace Troy_Web_Property_Manager.Tests.Controllers
{
    /// <summary>
    /// The request checks the editor actions make before calling the service. Each of these is rejected before the
    /// service is touched, so the controller doesn't need real services.
    /// </summary>
    public class ApplicationsControllerTests
    {
        private static ApplicationsController Controller()
        {
            return new(null!, null!);
        }

        [Theory]
        [InlineData(ApplicationSection.ApplicantInformation)]
        [InlineData(ApplicationSection.ResidenceHistory)]
        public async Task Submit_FromAnEditableSection_IsBadRequest(ApplicationSection section)
        {
            var result = await Controller().Edit(1, new ApplicationEditorViewModel { Section = section }, "submit");

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task EditPost_UndefinedSection_IsBadRequest()
        {
            var result = await Controller().Edit(1, new ApplicationEditorViewModel { Section = (ApplicationSection)99 }, "save");

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task EditGet_UndefinedSection_IsBadRequest()
        {
            Assert.IsType<BadRequestResult>(await Controller().Edit(1, (ApplicationSection)99));
        }

        [Fact]
        public async Task EditGet_SectionThatDidNotBind_IsBadRequest()
        {
            var controller = Controller();
            controller.ModelState.AddModelError("section", "The value 'abc' is not valid.");

            Assert.IsType<BadRequestResult>(await controller.Edit(1, ApplicationSection.Summary));
        }

        [Fact]
        public async Task Continue_FromTheSummary_IsBadRequest()
        {
            // There's no Continue button on the Summary, so this is a hand-made post.
            var result = await Controller().Edit(1, new ApplicationEditorViewModel { Section = ApplicationSection.Summary }, "continue");

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task UnknownCommand_IsBadRequest()
        {
            var result = await Controller().Edit(1, new ApplicationEditorViewModel { Section = ApplicationSection.ApplicantInformation }, "approve");

            Assert.IsType<BadRequestResult>(result);
        }
    }

    /// <summary>
    /// How the actions turn each kind of service result into a response: 404 for anything missing or someone else's,
    /// 403 for the wrong role, 409 for a conflict and 422 for a refusal, all through the real services and database.
    /// </summary>
    public sealed class ApplicationsControllerResultTests : IDisposable
    {
        private readonly TestDatabase _db = new();

        public void Dispose()
        {
            _db.Dispose();
        }

        // ---------------- Helpers ----------------

        private ApplicationService Service()
        {
            return new(_db.CreateContext());
        }

        private ApplicationsController Controller(CurrentUser user)
        {
            return new ApplicationsController(Service(), new PropertyService(_db.CreateContext())).SignedInAs(user);
        }

        private static void AssertOk(ServiceResult result)
        {
            Assert.True(result.Succeeded, $"NotFound={result.NotFound}; {string.Join("; ", result.Errors.Values)}");
        }

        private static ResidenceViewModel Residence(Guid version, string? landlordName = "Pat Landlord")
        {
            return new()
            {
                SectionVersion = version,
                Address = "5 Elm St",
                LandlordName = landlordName,
                LandlordPhone = "518-555-0199",
                MoveInDate = new DateOnly(2020, 1, 1),
                MoveOutDate = new DateOnly(2024, 12, 31)
            };
        }

        private async Task<int> StartAsync(int? unitId = null)
        {
            var result = await Service().StartAsync(unitId ?? _db.UnitId, ApplicantUser);
            AssertOk(result);
            return result.Id;
        }

        private async Task<ApplicationEditorViewModel> EditorAsync(int id)
        {
            return (await Service().GetEditorAsync(id, null, ApplicantUser))!;
        }

        private async Task<int> SubmittedAsync()
        {
            var id = await StartAsync();
            var editor = await EditorAsync(id);
            AssertOk(await Service().SaveApplicantInformationAsync(id, new ApplicantInformationViewModel
            {
                Name = "Alex Applicant",
                Phone = "518-555-0100",
                Email = "alex@example.com",
                CurrentAddress = "9 Oak Ave"
            }, editor.ApplicantInformationVersion, ApplicantUser));
            var saved = await Service().SaveResidenceAsync(id, Residence(editor.ResidenceHistoryVersion), ApplicantUser);
            AssertOk(saved);
            AssertOk(await Service().SaveResidenceHistoryAsync(id, saved.Version!.Value, ApplicantUser));
            editor = await EditorAsync(id);
            AssertOk(await Service().SubmitAsync(id, editor.ApplicantInformationVersion, editor.ResidenceHistoryVersion, ApplicantUser));
            return id;
        }

        private static int StatusOf(IActionResult result)
        {
            return Assert.IsType<PartialViewResult>(result).StatusCode ?? StatusCodes.Status200OK;
        }

        // ---------------- Start and the editor ----------------

        [Fact]
        public async Task Start_UnknownUnit_IsNotFound()
        {
            Assert.IsType<NotFoundResult>(await Controller(ApplicantUser).Start(9999));
        }

        [Fact]
        public async Task Continue_OnSomeoneElsesApplication_IsNotFound()
        {
            var id = await StartAsync();
            var model = new ApplicationEditorViewModel { Section = ApplicationSection.ApplicantInformation, ApplicantInformation = new() };

            Assert.IsType<NotFoundResult>(await Controller(ThirdApplicantUser).Edit(id, model, "continue"));
        }

        [Fact]
        public async Task Continue_ResidenceHistory_WhenSomeoneElseSavedIt_RedisplaysWithTheGeneralError()
        {
            var id = await StartAsync();
            var controller = Controller(ApplicantUser);
            // Section 1 wasn't on this page, so whatever bound for it must not show.
            controller.ModelState.AddModelError("ApplicantInformation.Name", "The Name field is required.");

            var result = await controller.Edit(id,
                new ApplicationEditorViewModel { Section = ApplicationSection.ResidenceHistory, ResidenceHistoryVersion = Guid.NewGuid() }, "continue");

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal(nameof(ApplicationsController.Edit), view.ViewName);
            Assert.Equal(ApplicationService.SectionChangedMessage, Assert.Single(controller.ModelState[""]!.Errors).ErrorMessage);
            Assert.DoesNotContain(controller.ModelState.Keys, k => k.StartsWith("ApplicantInformation."));
        }

        [Fact]
        public async Task Submit_SomeoneElsesApplication_IsNotFound()
        {
            var id = await StartAsync();

            var result = await Controller(ThirdApplicantUser).Edit(id, new ApplicationEditorViewModel { Section = ApplicationSection.Summary }, "submit");

            Assert.IsType<NotFoundResult>(result);
        }

        // ---------------- Residences ----------------

        [Fact]
        public async Task Residences_SomeoneElsesApplication_IsNotFound()
        {
            var id = await StartAsync();

            Assert.IsType<NotFoundResult>(await Controller(ThirdApplicantUser).Residences(id));
        }

        [Fact]
        public async Task ResidenceForm_UnknownResidence_IsNotFound()
        {
            var id = await StartAsync();

            Assert.IsType<NotFoundResult>(await Controller(ApplicantUser).Residence(id, residenceId: 9999));
        }

        [Fact]
        public async Task ResidenceForm_SavedWithErrors_ShowsThemUnderTheirFields()
        {
            var id = await StartAsync();
            var saved = await Service().SaveResidenceAsync(id, Residence((await EditorAsync(id)).ResidenceHistoryVersion, landlordName: ""), ApplicantUser);
            AssertOk(saved);
            var controller = Controller(ApplicantUser);

            var result = await controller.Residence(id, saved.Id);

            Assert.IsType<PartialViewResult>(result);
            Assert.True(controller.ModelState.ContainsKey(nameof(ResidenceViewModel.LandlordName)));
        }

        [Fact]
        public async Task SaveResidence_SomeoneElsesApplication_IsNotFound()
        {
            var id = await StartAsync();

            Assert.IsType<NotFoundResult>(await Controller(ThirdApplicantUser).Residence(id, Residence(Guid.NewGuid())));
        }

        [Fact]
        public async Task DeleteResidence_SomeoneElsesApplication_IsNotFound()
        {
            var id = await StartAsync();

            Assert.IsType<NotFoundResult>(await Controller(ThirdApplicantUser).DeleteResidenceConfirmed(id, 1, Guid.NewGuid()));
        }

        [Fact]
        public async Task DeleteResidence_AtTheCurrentVersion_ClosesTheModal_AndWhenStale_Is409()
        {
            var id = await StartAsync();
            var saved = await Service().SaveResidenceAsync(id, Residence((await EditorAsync(id)).ResidenceHistoryVersion), ApplicantUser);
            AssertOk(saved);

            var stale = await Controller(ApplicantUser).DeleteResidenceConfirmed(id, saved.Id, Guid.NewGuid());
            Assert.Equal(StatusCodes.Status409Conflict, StatusOf(stale));

            var removed = await Controller(ApplicantUser).DeleteResidenceConfirmed(id, saved.Id, saved.Version!.Value);
            Assert.IsType<JsonResult>(removed);
            Assert.Empty(_db.CreateContext().Residences);
        }

        // ---------------- Applicants ----------------

        [Fact]
        public async Task Applicants_SomeoneElsesApplication_IsNotFound()
        {
            var id = await StartAsync();

            Assert.IsType<NotFoundResult>(await Controller(ThirdApplicantUser).Applicants(id));
        }

        [Fact]
        public async Task AddApplicant_ThatDidNotValidate_RedrawsTheForm()
        {
            var id = await StartAsync();
            var controller = Controller(ApplicantUser);
            controller.ModelState.AddModelError(nameof(AddApplicantViewModel.Email), "The Email field is not a valid e-mail address.");

            var result = await controller.AddApplicant(id, new AddApplicantViewModel { Email = "not-an-email" });

            Assert.Equal(StatusCodes.Status422UnprocessableEntity, StatusOf(result));
        }

        [Fact]
        public async Task AddApplicant_SomeoneElsesApplication_IsNotFound()
        {
            var id = await StartAsync();

            var result = await Controller(ThirdApplicantUser).AddApplicant(id, new AddApplicantViewModel { Email = $"{OtherApplicantUser.Id}@example.com" });

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task RemoveApplicantForm_TheStarterOrSomeoneNotOnIt_IsNotFound()
        {
            var id = await StartAsync();
            var starter = (await EditorAsync(id)).Applicants.Single().ApplicantId;

            Assert.IsType<NotFoundResult>(await Controller(ApplicantUser).RemoveApplicant(id, starter));
            Assert.IsType<NotFoundResult>(await Controller(ApplicantUser).RemoveApplicant(id, 9999));
        }

        [Fact]
        public async Task RemoveApplicant_SomeoneElsesApplication_IsNotFound()
        {
            var id = await StartAsync();

            Assert.IsType<NotFoundResult>(await Controller(ThirdApplicantUser).RemoveApplicantConfirmed(id, 1));
        }

        [Fact]
        public async Task RemoveApplicant_Refused_RedrawsTheConfirmationWithTheReason()
        {
            // Submitted, so no one can be removed; the applicant id isn't on it either, so the modal names no one.
            var id = await SubmittedAsync();
            var controller = Controller(ApplicantUser);

            var result = await controller.RemoveApplicantConfirmed(id, 9999);

            Assert.Equal(StatusCodes.Status422UnprocessableEntity, StatusOf(result));
            var confirm = Assert.IsType<ConfirmViewModel>(((PartialViewResult)result).Model);
            Assert.Equal("Remove this applicant from this application? They won't be able to see it any more.", confirm.Message);
            Assert.Equal("This application can no longer be edited.", Assert.Single(controller.ModelState[""]!.Errors).ErrorMessage);
        }

        // ---------------- Withdraw ----------------

        [Fact]
        public async Task Withdraw_AlreadyWithdrawn_RedrawsTheConfirmationWithTheReason()
        {
            var id = await StartAsync();
            AssertOk(await Service().WithdrawAsync(id, ApplicantUser));

            var result = await Controller(ApplicantUser).WithdrawConfirmed(id);

            Assert.Equal(StatusCodes.Status422UnprocessableEntity, StatusOf(result));
        }

        // ---------------- Managers ----------------

        [Fact]
        public async Task Claim_UnknownApplication_IsNotFound()
        {
            Assert.IsType<NotFoundResult>(await Controller(ManagerUser).Claim(9999));
        }

        [Fact]
        public async Task Claim_ByAnApplicant_IsForbidden()
        {
            var id = await SubmittedAsync();

            Assert.IsType<ForbidResult>(await Controller(ApplicantUser).Claim(id));
        }

        [Fact]
        public async Task ReleaseForm_NotUnderReview_IsNotFound()
        {
            var id = await SubmittedAsync();

            Assert.IsType<NotFoundResult>(await Controller(ManagerUser).Release(id));
            Assert.IsType<NotFoundResult>(await Controller(ManagerUser).Release(9999));
        }

        [Fact]
        public async Task ReleaseForm_AnotherManagersClaim_SaysWhoseItIs()
        {
            var id = await SubmittedAsync();
            AssertOk(await Service().ClaimAsync(id, OtherManagerUser));

            var result = await Controller(ManagerUser).Release(id);

            var confirm = Assert.IsType<ConfirmViewModel>(Assert.IsType<PartialViewResult>(result).Model);
            Assert.Equal($"This application is claimed by {OtherManagerUser.Id}@example.com. Release it back to the review queue?", confirm.Message);
        }

        [Fact]
        public async Task ReleaseForm_OwnClaim_JustAsksToConfirm()
        {
            var id = await SubmittedAsync();
            AssertOk(await Service().ClaimAsync(id, ManagerUser));

            var result = await Controller(ManagerUser).Release(id);

            var confirm = Assert.IsType<ConfirmViewModel>(Assert.IsType<PartialViewResult>(result).Model);
            Assert.Equal("Release this application back to the review queue? Another manager can then claim it.", confirm.Message);
        }

        [Fact]
        public async Task Release_UnknownApplication_IsNotFound()
        {
            Assert.IsType<NotFoundResult>(await Controller(ManagerUser).ReleaseConfirmed(9999));
        }

        [Fact]
        public async Task Release_NotUnderReview_RedrawsTheConfirmationWithTheReason()
        {
            var id = await SubmittedAsync();
            var controller = Controller(ManagerUser);

            var result = await controller.ReleaseConfirmed(id);

            Assert.Equal(StatusCodes.Status422UnprocessableEntity, StatusOf(result));
            Assert.Equal("Only applications under review can be released.", Assert.Single(controller.ModelState[""]!.Errors).ErrorMessage);
        }

        [Fact]
        public async Task Review_UnknownApplication_IsNotFound()
        {
            var result = await Controller(ManagerUser).Review(9999, new ReviewViewModel { Outcome = ReviewOutcome.Approve });

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ManagerNotes_UnknownApplication_IsNotFound()
        {
            Assert.IsType<NotFoundResult>(await Controller(ManagerUser).ManagerNotes(9999));
            Assert.IsType<NotFoundResult>(await Controller(ManagerUser).EditManagerNotes(9999));
        }

        [Fact]
        public async Task SaveManagerNotes_ByAnApplicant_IsForbidden()
        {
            var id = await SubmittedAsync();

            var result = await Controller(ApplicantUser).EditManagerNotes(id, new ManagerNotesViewModel { Notes = "Mine." });

            Assert.IsType<ForbidResult>(result);
            Assert.Empty(await _db.CreateContext().ManagerNotes.ToListAsync());
        }
    }
}
