using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Rules;
using Troy_Web_Property_Manager.Services;
using Troy_Web_Property_Manager.ViewModels;

namespace Troy_Web_Property_Manager.Controllers
{
    /// <summary>
    /// Rental applications: the list, the one-page editor, the applicants on it, and the residence/withdraw/review
    /// modals.
    /// <para>
    /// Security is layered. [Authorize] on the class lets in only our two roles, and each action narrows it down
    /// (applicants: Start, Edit POST, Residence, Withdraw; managers: Queue, Claim, Release, Review). Not logged in? You get sent to the login
    /// page. Wrong role? 403.
    /// </para>
    /// <para>
    /// Roles alone can't stop applicant A from opening applicant B's application, so everything goes through
    /// ApplicationService, which filters to the applications you're on (as the one who started it or an added
    /// applicant). Someone else's application comes back as 404 rather than 403 so we
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
        // Section 1's ModelState keys start with this, so the service's field errors land on the right inputs.
        private const string InfoPrefix = ApplicationService.ApplicantInformationPrefix;

        // What the residence modal refreshes when it saves. Has to match the root id in _ResidenceHistory.cshtml.
        private const string ResidenceHistoryTarget = "#residence-history";

        // What the add/remove applicant modals refresh. Has to match the root id in _Applicants.cshtml.
        private const string ApplicantsTarget = "#application-applicants";

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
            if (model is null) return NotFound();
            // Errors still on the saved section go into ModelState, so each shows under its own field - on section 1
            // and on the Summary. (Residence errors are shown on their rows from the view model.)
            foreach (var error in model.ApplicantInformationErrors)
            {
                ModelState.AddModelError(error.Field, error.Message);
            }
            return View(model);
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
                    // 4.b.i: save this section - even if it breaks its rules - then move on if it's clean.
                    // We don't check ModelState here: the service runs the section's rules on what it saved.
                    ServiceResult result;
                    if (model.Section == ApplicationSection.ApplicantInformation)
                    {
                        // The version the page was loaded with - stale if another applicant saved this section since.
                        result = await applications.SaveApplicantInformationAsync(id, model.ApplicantInformation,
                            model.ApplicantInformationVersion, CurrentUser);
                    }
                    else if (model.Section == ApplicationSection.ResidenceHistory)
                    {
                        // The modal already saved each residence. Here we just mark the section as saved.
                        result = await applications.SaveResidenceHistoryAsync(id, model.ResidenceHistoryVersion, CurrentUser);
                    }
                    else return BadRequest(); // there's no Continue on the Summary, so someone's crafting posts

                    if (IsAccessFailure(result)) return Failure(result);
                    if (result.Succeeded)
                    {
                        if (result.Unresolved.Count == 0)
                        {
                            return RedirectToAction(nameof(Edit), new { id, section = ApplicationWorkflow.Next(model.Section) });
                        }
                        // Saved with errors: stay on this section. The GET puts the errors on their fields (from what
                        // was saved, so a refresh shows the same thing), and the page offers Next to carry on anyway.
                        SetError("Saved, but some fields still need fixing before you can submit.");
                        return RedirectToAction(nameof(Edit), new { id, section = model.Section });
                    }

                    // Not saved (too long for the database, "no longer editable", someone else saved it first): show the
                    // same section with what they typed and the errors. The hidden version keeps its posted (old) value
                    // from ModelState, so after a stale save, Continue stays stale until they reload.
                    AddErrors(result, model.Section == ApplicationSection.ApplicantInformation ? InfoPrefix : "");
                    return await RedisplayAsync(id, model);

                case "submit":
                    // 4.b.ii / 4.e: the service makes sure both sections are saved and the unit isn't already leased,
                    // and that neither section changed since this Summary was loaded.
                    var submitted = await applications.SubmitAsync(id, model.ApplicantInformationVersion,
                        model.ResidenceHistoryVersion, CurrentUser);
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
        /// The page for this user if they can edit it (they're on it, and it's Draft or Returned), otherwise null. We
        /// check before showing a form so a read-only application never opens an editable modal. It also gives the
        /// modals the section's current version.
        /// </summary>
        private async Task<ApplicationEditorViewModel?> EditableAsync(int id)
        {
            var model = await applications.GetEditorAsync(id, ApplicationSection.ResidenceHistory, CurrentUser);
            return model?.CanEdit == true ? model : null;
        }

        /// <summary>
        /// Residence form for the modal - add if there's no <paramref name="residenceId"/>, otherwise edit. When
        /// editing, any errors still on the saved residence show under their fields straight away. The form carries
        /// Residence History's version as of now, so saving is rejected if another applicant saves the section first.
        /// </summary>
        [Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> Residence(int id, int? residenceId)
        {
            var editor = await EditableAsync(id);
            if (editor is null) return NotFound();
            var model = residenceId is null
                ? new ResidenceViewModel { ApplicationId = id, SectionVersion = editor.ResidenceHistoryVersion }
                : await applications.GetResidenceAsync(id, residenceId.Value, CurrentUser);
            if (model is null) return NotFound();
            foreach (var error in model.Errors)
            {
                ModelState.AddModelError(error.Field, error.Message);
            }
            return PartialView("_ResidenceForm", model);
        }

        /// <summary>
        /// Saves a residence from the modal - even if it breaks the residence rules. Clean: the modal closes and the
        /// residence list refreshes. Saved with errors: the modal stays open on the saved residence with the errors
        /// under their fields, and the list behind it refreshes too. Only text too long for the database isn't saved.
        /// </summary>
        [HttpPost, Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> Residence(int id, ResidenceViewModel model)
        {
            // Always take the application id from the route (ApplicationId is [BindNever]). We don't check ModelState -
            // the service runs the residence rules on what it saves.
            model.ApplicationId = id;

            var result = await applications.SaveResidenceAsync(id, model, CurrentUser);
            if (IsAccessFailure(result)) return Failure(result);
            if (!result.Succeeded) return ModalFailed("_ResidenceForm", model, result);
            if (result.Unresolved.Count == 0) return ModalSuccess(ResidenceHistoryTarget, Url.Action(nameof(Residences), new { id }));

            // Redraw on the saved residence, so saving again edits it instead of adding a second one. Clearing
            // ModelState drops the posted ResidenceId and SectionVersion (the hidden fields would otherwise keep the
            // old values) and any binding errors, which the residence rules report in plainer words anyway. The new
            // version means this applicant's own save doesn't make their next one look stale.
            ModelState.Clear();
            model.ResidenceId = result.Id;
            model.SectionVersion = result.Version!.Value;
            model.SavedWithErrors = true;
            foreach (var error in result.Unresolved)
            {
                ModelState.AddModelError(error.Field, error.Message);
            }
            return PartialView("_ResidenceForm", model);
        }

        /// <summary>"Are you sure?" modal for removing a residence (uses the shared _Confirm partial).</summary>
        [Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> DeleteResidence(int id, int residenceId)
        {
            var editor = await EditableAsync(id);
            if (editor is null || await applications.GetResidenceAsync(id, residenceId, CurrentUser) is null) return NotFound();
            return PartialView("_Confirm", ConfirmDeleteResidence(id, residenceId, editor.ResidenceHistoryVersion));
        }

        /// <summary>
        /// Actually removes the residence. <c>[ActionName]</c> lets the GET and POST share a URL, since C# won't let
        /// two methods have the same signature. <paramref name="version"/> is Residence History's version when the
        /// confirmation opened (it's in the post URL), so it's rejected if another applicant saved the section since.
        /// </summary>
        [HttpPost, ActionName(nameof(DeleteResidence)), Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> DeleteResidenceConfirmed(int id, int residenceId, Guid version)
        {
            var result = await applications.DeleteResidenceAsync(id, residenceId, version, CurrentUser);
            if (IsAccessFailure(result)) return Failure(result);
            if (result.Succeeded) return ModalSuccess(ResidenceHistoryTarget, Url.Action(nameof(Residences), new { id }));
            return ModalFailed("_Confirm", ConfirmDeleteResidence(id, residenceId, version), result);
        }

        private ConfirmViewModel ConfirmDeleteResidence(int id, int residenceId, Guid version)
        {
            return new("Remove residence", "Remove this residence?", Url.Action(nameof(DeleteResidence), new { id, residenceId, version })!, "Remove");
        }

        // ---------------- Applicants on the application (modals) ----------------
        // Any applicant on the application can add another applicant by email, or remove one (or leave), while it can
        // still be edited. The service checks all of that; these actions just drive the modals.

        /// <summary>Just the applicants panel. site.js calls this to refresh it after adding or removing someone.</summary>
        public async Task<IActionResult> Applicants(int id)
        {
            var model = await applications.GetEditorAsync(id, null, CurrentUser);
            return model is null ? NotFound() : PartialView("_Applicants", model);
        }

        /// <summary>"Add applicant" form for the modal.</summary>
        [Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> AddApplicant(int id)
        {
            if (await EditableAsync(id) is null) return NotFound();
            return PartialView("_AddApplicantForm", new AddApplicantViewModel { ApplicationId = id });
        }

        [HttpPost, Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> AddApplicant(int id, AddApplicantViewModel model)
        {
            model.ApplicationId = id;
            if (!ModelState.IsValid) return ModalInvalid("_AddApplicantForm", model);

            var result = await applications.AddApplicantAsync(id, model, CurrentUser);
            if (IsAccessFailure(result)) return Failure(result);
            if (result.Succeeded) return ModalSuccess(ApplicantsTarget, Url.Action(nameof(Applicants), new { id }));
            return ModalFailed("_AddApplicantForm", model, result);
        }

        /// <summary>"Are you sure?" modal for removing an applicant, or leaving when it's yourself.</summary>
        [Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> RemoveApplicant(int id, int applicantId)
        {
            var applicant = (await EditableAsync(id))?.Applicants.FirstOrDefault(a => a.ApplicantId == applicantId);
            if (applicant is null || applicant.IsStarter) return NotFound();
            return PartialView("_Confirm", ConfirmRemoveApplicant(id, applicant));
        }

        [HttpPost, ActionName(nameof(RemoveApplicant)), Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> RemoveApplicantConfirmed(int id, int applicantId)
        {
            // Look them up before removing, to know if they're leaving (they can't see the page afterwards).
            var applicant = (await applications.GetEditorAsync(id, null, CurrentUser))?.Applicants.FirstOrDefault(a => a.ApplicantId == applicantId);
            var result = await applications.RemoveApplicantAsync(id, applicantId, CurrentUser);
            if (IsAccessFailure(result)) return Failure(result);
            if (result.Succeeded)
            {
                if (applicant?.IsYou == true)
                {
                    SetMessage("You left the application.");
                    return ModalRedirect(Url.Action(nameof(Index))!);
                }
                return ModalSuccess(ApplicantsTarget, Url.Action(nameof(Applicants), new { id }));
            }
            return ModalFailed("_Confirm", ConfirmRemoveApplicant(id, applicant ?? new ApplicationApplicantViewModel { ApplicantId = applicantId }), result);
        }

        private ConfirmViewModel ConfirmRemoveApplicant(int id, ApplicationApplicantViewModel applicant)
        {
            var action = Url.Action(nameof(RemoveApplicant), new { id, applicantId = applicant.ApplicantId })!;
            return applicant.IsYou
                ? new("Leave application", "Leave this application? You won't be able to see it any more unless someone adds you back.", action, "Leave")
                : new("Remove applicant", $"Remove {applicant.Email} from this application? They won't be able to see it any more.", action, "Remove");
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

        // ---------------- Review queue (property managers) ----------------
        // A manager claims a Submitted application (it goes Under Review) before reviewing it, so two managers never
        // work on the same one. They can release it back to the queue without a decision.

        /// <summary>The queue: my claims, what's waiting (oldest first), and what other managers have claimed.</summary>
        [Authorize(Roles = AppRoles.PropertyManager)]
        public async Task<IActionResult> Queue()
        {
            return View(await applications.GetQueueAsync(CurrentUser));
        }

        /// <summary>
        /// Claims a Submitted application. A plain form post from the queue or the application page; either way we
        /// land on the application page, where the Review button is now available (or the reason it isn't).
        /// </summary>
        [HttpPost, Authorize(Roles = AppRoles.PropertyManager)]
        public async Task<IActionResult> Claim(int id)
        {
            var result = await applications.ClaimAsync(id, CurrentUser);
            if (IsAccessFailure(result)) return Failure(result);
            if (result.Succeeded) SetMessage("You claimed this application. It's now under review.");
            else SetError(result.Errors.Values.First()); // e.g. another manager claimed it first
            return RedirectToAction(nameof(Edit), new { id });
        }

        /// <summary>"Are you sure?" modal for releasing a claim. Warns when it's someone else's claim.</summary>
        [Authorize(Roles = AppRoles.PropertyManager)]
        public async Task<IActionResult> Release(int id)
        {
            var model = await applications.GetEditorAsync(id, null, CurrentUser);
            if (model?.CanRelease != true) return NotFound();
            return PartialView("_Confirm", ConfirmRelease(id, model.ClaimedByMe ? null : model.Reviewer));
        }

        [HttpPost, ActionName(nameof(Release)), Authorize(Roles = AppRoles.PropertyManager)]
        public async Task<IActionResult> ReleaseConfirmed(int id)
        {
            var result = await applications.ReleaseAsync(id, CurrentUser);
            if (IsAccessFailure(result)) return Failure(result);
            if (result.Succeeded)
            {
                SetMessage("The application was released back to the review queue.");
                return ModalSuccess(); // reloads the page (the application or the queue)
            }
            return ModalFailed("_Confirm", ConfirmRelease(id, null), result);
        }

        private ConfirmViewModel ConfirmRelease(int id, string? otherReviewer)
        {
            var message = otherReviewer is null
                ? "Release this application back to the review queue? Another manager can then claim it."
                : $"This application is claimed by {otherReviewer}. Release it back to the review queue?";
            return new("Release application", message, Url.Action(nameof(Release), new { id })!, "Release");
        }

        // ---------------- Review (modal, property managers, 5.a) ----------------

        /// <summary>Review form for the modal. Only works on an application this manager has claimed.</summary>
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
