using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Rules;
using Troy_Web_Property_Manager.Services;
using Troy_Web_Property_Manager.ViewModels;

namespace Troy_Web_Property_Manager.Controllers
{
    /// <summary>
    /// Rental applications: the list, the one-page editor, and the residence/withdraw/review modals.
    /// <para>
    /// Security is layered. [Authorize] on the class lets in only our two roles, and each action narrows it down
    /// (applicants: Start, Edit POST, Residence, Withdraw; managers: Review). Not logged in? You get sent to the login
    /// page. Wrong role? 403.
    /// </para>
    /// <para>
    /// Roles alone can't stop applicant A from opening applicant B's application, so everything goes through
    /// ApplicationService, which filters by owner. Someone else's application comes back as 404 rather than 403 so we
    /// don't even admit it exists. The views hide buttons you shouldn't use, and the global filter in Program.cs checks
    /// the antiforgery token on every POST.
    /// </para>
    /// <para>
    /// I've kept the controllers thin: bind, validate, call the service, return a result. The actual business rules
    /// live in the service and the Rules classes so they can be unit tested without HTTP.
    /// </para>
    /// </summary>
    [Authorize(Roles = AppRoles.Applicant + "," + AppRoles.PropertyManager)]
    public class ApplicationsController(ApplicationService applications, PropertyService properties) : AppController
    {
        // Section 1's ModelState keys start with this, so Continue can validate just that section.
        private const string InfoPrefix = nameof(ApplicationEditorViewModel.ApplicantInformation) + ".";

        // What the residence modal refreshes when it saves. Has to match the root id in _ResidenceHistory.cshtml.
        private const string ResidenceHistoryTarget = "#residence-history";

        // What the notes modal refreshes when it saves. Has to match the root id in Components/ManagerNotes/Default.cshtml.
        private const string ManagerNotesTarget = "#manager-notes";

        // ---------------- List ----------------

        /// <summary>
        /// The application list. Applicants see their own, managers see everything.
        /// The status and property filters go into the query so SQL Server does the filtering, not us in memory.
        /// </summary>
        public async Task<IActionResult> Index(ApplicationStatus? status, int? propertyId)
        {
            return View(new ApplicationListViewModel
            {
                Status = status,
                PropertyId = propertyId,
                Items = await applications.ListAsync(status, propertyId, CurrentUser),
                Properties = await properties.GetPropertyOptionsAsync(propertyId)
            });
        }

        // ---------------- Start ----------------

        /// <summary>
        /// Apply for a unit from the Available units page.
        /// It's a POST (not a GET) since it creates data, and that way the antiforgery token protects it too.
        /// If they apply for the same unit again, we just reopen their existing application.
        /// </summary>
        [HttpPost, Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> Start(int unitId)
        {
            var result = await applications.StartAsync(unitId, CurrentUser);
            if (IsAccessFailure(result)) return Failure(result);
            if (!result.Succeeded)
            {
                // e.g. someone leased the unit after the page loaded - send them back to the list and say why.
                SetError(result.Errors.Values.First());
                return RedirectToAction(nameof(UnitsController.Index), "Units");
            }
            return RedirectToAction(nameof(Edit), new { id = result.Id });
        }

        // ---------------- Editor (4.b - one page, one view model, one form, one action) ----------------

        /// <summary>
        /// Shows the application page, one section at a time. <paramref name="section"/> is from the query string;
        /// if it's missing, editors land on section 1 and everyone else on the Summary. 404 if the application
        /// doesn't exist or isn't yours.
        /// </summary>
        public async Task<IActionResult> Edit(int id, ApplicationSection? section)
        {
            var model = await applications.GetEditorAsync(id, section, CurrentUser);
            return model is null ? NotFound() : View(model);
        }

        /// <summary>
        /// Every button in the editor posts here (4.b). The button's <c>name="command"</c> value tells us what to do.
        /// </summary>
        /// <remarks>
        /// <para>Applicants only - managers never post to the editor, they use the review modal.</para>
        /// <para>On a successful Continue/Submit we redirect (Post/Redirect/Get) so hitting refresh doesn't re-post.
        /// If something fails we render the view directly so the user keeps what they typed and sees the errors.</para>
        /// </remarks>
        [HttpPost, Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> Edit(int id, ApplicationEditorViewModel model, string command)
        {
            switch (command)
            {
                case "back":
                    // 4.b.ii: Back just goes to the previous section. No save, no validation (the button has
                    // formnovalidate so the browser doesn't get in the way either).
                    return RedirectToAction(nameof(Edit), new { id, section = ApplicationWorkflow.Previous(model.Section) });

                case "continue":
                    // 4.b.i: validate this section, save it if it's good, then move on to the next one.
                    ServiceResult result;
                    if (model.Section == ApplicationSection.ApplicantInformation)
                    {
                        // Only look at section 1's fields. ModelState has entries for the rest of the view model too,
                        // and we don't want those failing this section. If it's invalid, show it again with errors.
                        if (!SectionIsValid(InfoPrefix)) return await RedisplayAsync(id, model);
                        result = await applications.SaveApplicantInformationAsync(id, model.ApplicantInformation, CurrentUser);
                    }
                    else if (model.Section == ApplicationSection.ResidenceHistory)
                    {
                        // The modal already saved each residence. Here we just check there's at least one and mark
                        // the section as saved.
                        result = await applications.SaveResidenceHistoryAsync(id, CurrentUser);
                    }
                    else return BadRequest(); // there's no Continue on the Summary, so someone's crafting posts

                    if (IsAccessFailure(result)) return Failure(result);
                    if (result.Succeeded) return RedirectToAction(nameof(Edit), new { id, section = ApplicationWorkflow.Next(model.Section) });

                    // Errors from the service (e.g. "no longer editable", someone else changed it) show on the same section.
                    AddErrors(result, model.Section == ApplicationSection.ApplicantInformation ? InfoPrefix : "");
                    return await RedisplayAsync(id, model);

                case "submit":
                    // 4.b.ii / 4.e: the service makes sure both sections are saved and the unit isn't already leased.
                    var submitted = await applications.SubmitAsync(id, CurrentUser);
                    if (IsAccessFailure(submitted)) return Failure(submitted);
                    if (submitted.Succeeded)
                    {
                        SetMessage("Your application was submitted.");
                        return RedirectToAction(nameof(Edit), new { id });
                    }
                    AddErrors(submitted);
                    return await RedisplayAsync(id, model);

                default:
                    // Our buttons never send anything else, so this is a hand-made request.
                    return BadRequest();
            }
        }

        /// <summary>True if nothing in ModelState under this prefix is invalid.</summary>
        private bool SectionIsValid(string prefix)
        {
            return ModelState.Where(e => e.Key.StartsWith(prefix)).All(e => e.Value!.ValidationState != ModelValidationState.Invalid);
        }

        /// <summary>
        /// Shows the same section again with its errors, keeping what the user typed (4.b.i).
        /// </summary>
        /// <remarks>
        /// Everything else on the page (unit label, residences, permission flags) gets reloaded from the database,
        /// not taken from the post - so a bad post can't change what the page shows or lets you do. We only copy
        /// back the section 1 values the user typed.
        /// </remarks>
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
                // (If we left them, the tag helpers would show the empty posted values instead of the saved ones.)
                foreach (var key in ModelState.Keys.Where(k => k.StartsWith(InfoPrefix)).ToList())
                {
                    ModelState.Remove(key);
                }
            }

            return View(nameof(Edit), model);
        }

        // ---------------- Residences (modal, 4.c) ----------------
        // Same modal pattern as everywhere else (Technical 1.b): the GET returns the form as a partial and site.js
        // drops it into the modal. The POST sends back the same partial with a 422 if validation fails, or JSON if it
        // worked - then the modal closes and #residence-history gets reloaded from Residences().

        /// <summary>Just the residence list partial. site.js calls this to refresh that part of the page.</summary>
        public async Task<IActionResult> Residences(int id)
        {
            var model = await applications.GetEditorAsync(id, ApplicationSection.ResidenceHistory, CurrentUser);
            return model is null ? NotFound() : PartialView("_ResidenceHistory", model);
        }

        /// <summary>
        /// Can this user edit the residences? (Has to be their application, and Draft or Returned.) We check before
        /// showing the form so a read-only application never opens an editable modal.
        /// </summary>
        private async Task<bool> CanEditAsync(int id)
        {
            return (await applications.GetEditorAsync(id, ApplicationSection.ResidenceHistory, CurrentUser))?.CanEdit == true;
        }

        /// <summary>Residence form for the modal - add if there's no <paramref name="residenceId"/>, otherwise edit.</summary>
        [Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> Residence(int id, int? residenceId)
        {
            if (!await CanEditAsync(id)) return NotFound();
            var model = residenceId is null
                ? new ResidenceViewModel { ApplicationId = id }
                : await applications.GetResidenceAsync(id, residenceId.Value, CurrentUser);
            return model is null ? NotFound() : PartialView("_ResidenceForm", model);
        }

        /// <summary>Saves a residence from the modal.</summary>
        [HttpPost, Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> Residence(int id, ResidenceViewModel model)
        {
            // Always take the application id from the route (ApplicationId is [BindNever]).
            model.ApplicationId = id;
            if (!ModelState.IsValid) return ModalInvalid("_ResidenceForm", model);

            var result = await applications.SaveResidenceAsync(id, model, CurrentUser);
            if (IsAccessFailure(result)) return Failure(result);
            if (result.Succeeded) return ModalSuccess(ResidenceHistoryTarget, Url.Action(nameof(Residences), new { id }));
            return ModalFailed("_ResidenceForm", model, result);
        }

        /// <summary>"Are you sure?" modal for removing a residence (uses the shared _Confirm partial).</summary>
        [Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> DeleteResidence(int id, int residenceId)
        {
            if (!await CanEditAsync(id) || await applications.GetResidenceAsync(id, residenceId, CurrentUser) is null) return NotFound();
            return PartialView("_Confirm", ConfirmDeleteResidence(id, residenceId));
        }

        /// <summary>
        /// Actually removes the residence. <c>[ActionName]</c> lets the GET and POST share a URL, since C# won't let
        /// two methods have the same signature.
        /// </summary>
        [HttpPost, ActionName(nameof(DeleteResidence)), Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> DeleteResidenceConfirmed(int id, int residenceId)
        {
            var result = await applications.DeleteResidenceAsync(id, residenceId, CurrentUser);
            if (IsAccessFailure(result)) return Failure(result);
            if (result.Succeeded) return ModalSuccess(ResidenceHistoryTarget, Url.Action(nameof(Residences), new { id }));
            return ModalFailed("_Confirm", ConfirmDeleteResidence(id, residenceId), result);
        }

        private ConfirmViewModel ConfirmDeleteResidence(int id, int residenceId)
        {
            return new("Remove residence", "Remove this residence?", Url.Action(nameof(DeleteResidence), new { id, residenceId })!, "Remove");
        }

        // ---------------- Withdraw (modal, Challenge a) ----------------

        /// <summary>"Are you sure?" modal. 404 unless it's your application and it isn't already closed out.</summary>
        [Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> Withdraw(int id)
        {
            if ((await applications.GetEditorAsync(id, null, CurrentUser))?.CanWithdraw != true) return NotFound();
            return PartialView("_Confirm", ConfirmWithdraw(id));
        }

        [HttpPost, ActionName(nameof(Withdraw)), Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> WithdrawConfirmed(int id)
        {
            var result = await applications.WithdrawAsync(id, CurrentUser);
            if (IsAccessFailure(result)) return Failure(result);
            if (result.Succeeded)
            {
                SetMessage("Your application was withdrawn.");
                return ModalSuccess(); // reloads the page
            }
            return ModalFailed("_Confirm", ConfirmWithdraw(id), result);
        }

        // ---------------- Review (modal, property managers, 5.a) ----------------

        /// <summary>Review form for the modal. Only works on a Submitted application.</summary>
        [Authorize(Roles = AppRoles.PropertyManager)]
        public async Task<IActionResult> Review(int id)
        {
            if (!await applications.CanReviewAsync(id, CurrentUser)) return NotFound();
            return PartialView("_ReviewForm", new ReviewViewModel { ApplicationId = id });
        }

        /// <summary>
        /// Saves the review. Approve creates the 12-month lease (2.d); Return and Deny need a comment. The service
        /// checks for an active lease again at approval time so a unit can't end up with two (4.e).
        /// </summary>
        [HttpPost, Authorize(Roles = AppRoles.PropertyManager)]
        public async Task<IActionResult> Review(int id, ReviewViewModel model)
        {
            model.ApplicationId = id;
            if (!ModelState.IsValid) return ModalInvalid("_ReviewForm", model); // e.g. Deny without a comment

            var result = await applications.ReviewAsync(id, model, CurrentUser);
            if (IsAccessFailure(result)) return Failure(result);
            if (result.Succeeded)
            {
                SetMessage(model.Outcome switch
                {
                    ReviewOutcome.Approve => $"Application approved; a {LeaseRules.TermMonths}-month lease was created.",
                    ReviewOutcome.Return => "Application returned to the applicant.",
                    _ => "Application denied."
                });
                return ModalSuccess(); // status, buttons and history all change, so reload the page
            }
            return ModalFailed("_ReviewForm", model, result);
        }

        // ---------------- Manager notes (modal, property managers only) ----------------
        // Private notes applicants never see. Every action is locked to the Property Manager role, and the service
        // checks the role again, so an applicant gets a 403 here and nothing back from the service either way.

        /// <summary>Just the notes panel. site.js calls this to refresh that part of the page after a save.</summary>
        [Authorize(Roles = AppRoles.PropertyManager)]
        public async Task<IActionResult> ManagerNotes(int id)
        {
            var model = await applications.GetManagerNotesAsync(id, CurrentUser);
            // Same markup the ManagerNotes view component renders on the page.
            return model is null ? NotFound() : PartialView("Components/ManagerNotes/Default", model);
        }

        /// <summary>Notes form for the modal.</summary>
        [Authorize(Roles = AppRoles.PropertyManager)]
        public async Task<IActionResult> EditManagerNotes(int id)
        {
            var model = await applications.GetManagerNotesAsync(id, CurrentUser);
            return model is null ? NotFound() : PartialView("_ManagerNotesForm", model);
        }

        /// <summary>Saves the notes. If another manager saved first, the modal says so (409) instead of overwriting.</summary>
        [HttpPost, Authorize(Roles = AppRoles.PropertyManager)]
        public async Task<IActionResult> EditManagerNotes(int id, ManagerNotesViewModel model)
        {
            model.ApplicationId = id;
            if (!ModelState.IsValid) return ModalInvalid("_ManagerNotesForm", model);

            var result = await applications.SaveManagerNotesAsync(id, model, CurrentUser);
            if (IsAccessFailure(result)) return Failure(result);
            if (result.Succeeded) return ModalSuccess(ManagerNotesTarget, Url.Action(nameof(ManagerNotes), new { id }));
            return ModalFailed("_ManagerNotesForm", model, result);
        }

        private ConfirmViewModel ConfirmWithdraw(int id)
        {
            return new("Withdraw application", "Withdraw this application? This can't be undone.", Url.Action(nameof(Withdraw), new { id })!, "Withdraw");
        }
    }
}
