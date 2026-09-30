using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Rules;
using Troy_Web_Property_Manager.Services;
using Troy_Web_Property_Manager.ViewModels;

namespace Troy_Web_Property_Manager.Controllers
{
    /// <summary>Applicants see their own applications; property managers see all of them.</summary>
    [Authorize(Roles = AppRoles.Applicant + "," + AppRoles.PropertyManager)]
    public class ApplicationsController(ApplicationService applications, PropertyService properties) : AppController
    {
        private const string InfoPrefix = nameof(ApplicationEditorViewModel.ApplicantInformation) + ".";
        private const string ResidenceHistoryTarget = "#residence-history";

        // ---------------- List ----------------

        /// <summary>Applicants see their own applications, managers all of them; the filters run in SQL.</summary>
        public async Task<IActionResult> Index(ApplicationStatus? status, int? propertyId) => View(new ApplicationListViewModel
        {
            Status = status,
            PropertyId = propertyId,
            Items = await applications.ListAsync(status, propertyId, CurrentUser),
            Properties = await properties.GetPropertyOptionsAsync(propertyId)
        });

        // ---------------- Start ----------------

        /// <summary>Apply for a unit from the Available units page.</summary>
        [HttpPost, Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> Start(int unitId)
        {
            var result = await applications.StartAsync(unitId, CurrentUser);
            if (result.NotFound) return NotFound();
            if (!result.Succeeded)
            {
                SetError(result.Errors.Values.First());
                return RedirectToAction(nameof(UnitsController.Index), "Units");
            }
            return RedirectToAction(nameof(Edit), new { id = result.Id });
        }

        // ---------------- Editor ----------------

        public async Task<IActionResult> Edit(int id, ApplicationSection? section)
        {
            var model = await applications.GetEditorAsync(id, section, CurrentUser);
            return model is null ? NotFound() : View(model);
        }

        /// <summary>One form, one action: the clicked button's value decides what happens.</summary>
        [HttpPost, Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> Edit(int id, ApplicationEditorViewModel model, string command)
        {
            switch (command)
            {
                case "back":
                    // Back returns to the previous section without saving.
                    return RedirectToAction(nameof(Edit), new { id, section = ApplicationWorkflow.Previous(model.Section) });

                case "continue":
                    ServiceResult result;
                    if (model.Section == ApplicationSection.ApplicantInformation)
                    {
                        // Persist only when this section is valid; otherwise re-render it with the errors.
                        if (!SectionIsValid(InfoPrefix)) return await RedisplayAsync(id, model);
                        result = await applications.SaveApplicantInformationAsync(id, model.ApplicantInformation, CurrentUser);
                    }
                    else if (model.Section == ApplicationSection.ResidenceHistory)
                    {
                        result = await applications.SaveResidenceHistoryAsync(id, CurrentUser);
                    }
                    else return BadRequest();

                    if (result.NotFound) return NotFound();
                    if (result.Succeeded) return RedirectToAction(nameof(Edit), new { id, section = ApplicationWorkflow.Next(model.Section) });
                    AddErrors(result, model.Section == ApplicationSection.ApplicantInformation ? InfoPrefix : "");
                    return await RedisplayAsync(id, model);

                case "submit":
                    var submitted = await applications.SubmitAsync(id, CurrentUser);
                    if (submitted.NotFound) return NotFound();
                    if (submitted.Succeeded)
                    {
                        SetMessage("Your application was submitted.");
                        return RedirectToAction(nameof(Edit), new { id });
                    }
                    AddErrors(submitted);
                    return await RedisplayAsync(id, model);

                default:
                    return BadRequest();
            }
        }

        private bool SectionIsValid(string prefix) =>
            ModelState.Where(e => e.Key.StartsWith(prefix)).All(e => e.Value!.ValidationState != ModelValidationState.Invalid);

        /// <summary>Shows the same section again with its errors, keeping what the user typed.</summary>
        private async Task<IActionResult> RedisplayAsync(int id, ApplicationEditorViewModel posted)
        {
            var model = await applications.GetEditorAsync(id, posted.Section, CurrentUser);
            if (model is null) return NotFound();

            if (posted.Section == ApplicationSection.ApplicantInformation)
            {
                model.ApplicantInformation = posted.ApplicantInformation;
            }
            else
            {
                // Section 1 wasn't on the page, so drop its (empty) posted values and their errors.
                foreach (var key in ModelState.Keys.Where(k => k.StartsWith(InfoPrefix)).ToList())
                {
                    ModelState.Remove(key);
                }
            }

            return View(nameof(Edit), model);
        }

        // ---------------- Residences (modal) ----------------

        /// <summary>Returns the residence list partial; the modal script calls it to refresh the page region.</summary>
        public async Task<IActionResult> Residences(int id)
        {
            var model = await applications.GetEditorAsync(id, ApplicationSection.ResidenceHistory, CurrentUser);
            return model is null ? NotFound() : PartialView("_ResidenceHistory", model);
        }

        /// <summary>True when the current user may edit this application's residences (theirs, Draft or Returned).</summary>
        private async Task<bool> CanEditAsync(int id) =>
            (await applications.GetEditorAsync(id, ApplicationSection.ResidenceHistory, CurrentUser))?.CanEdit == true;

        [Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> Residence(int id, int? residenceId)
        {
            if (!await CanEditAsync(id)) return NotFound();
            var model = residenceId is null
                ? new ResidenceViewModel { ApplicationId = id }
                : await applications.GetResidenceAsync(id, residenceId.Value, CurrentUser);
            return model is null ? NotFound() : PartialView("_ResidenceForm", model);
        }

        [HttpPost, Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> Residence(int id, ResidenceViewModel model)
        {
            model.ApplicationId = id;
            if (!ModelState.IsValid) return ModalInvalid("_ResidenceForm", model);

            var result = await applications.SaveResidenceAsync(id, model, CurrentUser);
            if (result.NotFound) return NotFound();
            if (result.Succeeded) return ModalSuccess(ResidenceHistoryTarget, Url.Action(nameof(Residences), new { id }));
            AddErrors(result);
            return ModalInvalid("_ResidenceForm", model);
        }

        [Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> DeleteResidence(int id, int residenceId)
        {
            if (!await CanEditAsync(id) || await applications.GetResidenceAsync(id, residenceId, CurrentUser) is null) return NotFound();
            return PartialView("_Confirm", ConfirmDeleteResidence(id, residenceId));
        }

        [HttpPost, ActionName(nameof(DeleteResidence)), Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> DeleteResidenceConfirmed(int id, int residenceId)
        {
            var result = await applications.DeleteResidenceAsync(id, residenceId, CurrentUser);
            if (result.NotFound) return NotFound();
            if (result.Succeeded) return ModalSuccess(ResidenceHistoryTarget, Url.Action(nameof(Residences), new { id }));
            AddErrors(result);
            return ModalInvalid("_Confirm", ConfirmDeleteResidence(id, residenceId));
        }

        private ConfirmViewModel ConfirmDeleteResidence(int id, int residenceId) =>
            new("Remove residence", "Remove this residence?", Url.Action(nameof(DeleteResidence), new { id, residenceId })!, "Remove");

        // ---------------- Withdraw (modal) ----------------

        [Authorize(Roles = AppRoles.Applicant)]
        public IActionResult Withdraw(int id) => PartialView("_Confirm", ConfirmWithdraw(id));

        [HttpPost, ActionName(nameof(Withdraw)), Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> WithdrawConfirmed(int id)
        {
            var result = await applications.WithdrawAsync(id, CurrentUser);
            if (result.NotFound) return NotFound();
            if (result.Succeeded)
            {
                SetMessage("Your application was withdrawn.");
                return ModalSuccess(); // reloads the page
            }
            AddErrors(result);
            return ModalInvalid("_Confirm", ConfirmWithdraw(id));
        }

        // ---------------- Review (modal, property manager) ----------------

        [Authorize(Roles = AppRoles.PropertyManager)]
        public async Task<IActionResult> Review(int id)
        {
            if (!await applications.CanReviewAsync(id, CurrentUser)) return NotFound();
            return PartialView("_ReviewForm", new ReviewViewModel { ApplicationId = id });
        }

        [HttpPost, Authorize(Roles = AppRoles.PropertyManager)]
        public async Task<IActionResult> Review(int id, ReviewViewModel model)
        {
            model.ApplicationId = id;
            if (!ModelState.IsValid) return ModalInvalid("_ReviewForm", model); // e.g. Deny without a comment

            var result = await applications.ReviewAsync(id, model, CurrentUser);
            if (result.NotFound) return NotFound();
            if (result.Succeeded)
            {
                SetMessage(model.Outcome switch
                {
                    ReviewOutcome.Approve => "Application approved; a 12-month lease was created.",
                    ReviewOutcome.Return => "Application returned to the applicant.",
                    _ => "Application denied."
                });
                return ModalSuccess(); // status, buttons and history all change, so reload the page
            }
            AddErrors(result);
            return ModalInvalid("_ReviewForm", model);
        }

        private ConfirmViewModel ConfirmWithdraw(int id) =>
            new("Withdraw application", "Withdraw this application? This can't be undone.", Url.Action(nameof(Withdraw), new { id })!, "Withdraw");
    }
}
