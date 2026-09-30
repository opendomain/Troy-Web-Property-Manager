using Microsoft.AspNetCore.Mvc.ModelBinding;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Rules;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>
    /// The one view model behind the whole application page (4.b: "One view model drives it").
    /// </summary>
    /// <remarks>
    /// <para>
    /// The application page shows one section at a time: Applicant Information, Residence History, or the Summary.
    /// Instead of a view model (and page) per section, this class carries everything any section needs.
    /// <c>Views/Applications/Edit.cshtml</c> uses it as its model and hands the same object to whichever section
    /// partial is showing (<c>_ApplicantInformation</c>, <c>_ResidenceHistory</c> or <c>_Summary</c>). The Summary just
    /// reuses the two section partials, so it can't get out of sync with them.
    /// </para>
    ///
    /// <para>
    /// There's also just one form and one POST action. Edit.cshtml wraps the current section in a single &lt;form&gt;
    /// that posts to <c>ApplicationsController.Edit(int id, ApplicationEditorViewModel model, string command)</c>.
    /// All the buttons are <c>name="command"</c>, so whichever one was clicked tells the action what to do:
    /// <list type="bullet">
    ///   <item><c>continue</c>: validate this section and save it if it's good, then go to the next one (or the
    ///   Summary). If it's not valid, show the same section again with the errors (4.b.i).</item>
    ///   <item><c>back</c>: go to the previous section without saving (4.b.ii).</item>
    ///   <item><c>submit</c>: only on the Summary, and only enabled once both sections are saved (4.b.ii).</item>
    /// </list>
    /// Since the model binder fills this same class on the POST, a failed Continue can pass the posted values right
    /// back to the view (<c>ApplicationsController.RedisplayAsync</c>) and the user doesn't lose what they typed.
    /// </para>
    ///
    /// <para>
    /// Over-posting: the only things we ever bind from the request are <see cref="Id"/> (route), <see cref="Section"/>
    /// (hidden field) and the <see cref="ApplicantInformation"/> fields. Everything else is <c>[BindNever]</c> - it's
    /// display data or a permission flag that gets rebuilt from the database every request
    /// (<c>ApplicationService.GetEditorAsync</c>). So if someone crafts a POST with <c>CanEdit=true</c>,
    /// <c>Status=Approved</c> or a list of residences, the binder just ignores it. Residences don't go through this
    /// form at all - they have their own modal (4.c) and view model (<see cref="ResidenceViewModel"/>).
    /// </para>
    ///
    /// <para>
    /// Whether the page is editable is decided on the server (4.d). The service works out <see cref="CanEdit"/> and
    /// <see cref="IsReadOnly"/> from the status and the user's role, and the views just read them. Each section
    /// partial renders editable or read-only based on <see cref="IsReadOnly"/> (disabled &lt;fieldset&gt;, hidden
    /// edit buttons). That's only the UI side though - the actions are role-restricted and every write in
    /// <c>ApplicationService</c> re-checks ownership and status (<c>LoadEditableAsync</c>), so re-enabling the fields in
    /// dev tools won't get a post through on a submitted application.
    /// </para>
    ///
    /// <para>
    /// <see cref="CanSubmit"/>, <see cref="CanWithdraw"/> and <see cref="CanReview"/> are calculated from the saved
    /// flags and the workflow rules (<see cref="ApplicationWorkflow"/>) rather than set separately, so the buttons
    /// always match what the service will actually allow. They're get-only, so model binding can't touch them.
    /// </para>
    /// </remarks>
    public class ApplicationEditorViewModel
    {
        /// <summary>The application's id, from the route (<c>/Applications/Edit/{id}</c>) on both GET and POST.</summary>
        public int Id { get; set; }

        /// <summary>
        /// Which section is showing. It's a hidden field in Edit.cshtml so the POST knows which section Continue/Back
        /// was clicked on. It only picks what to validate and show - it doesn't grant anything.
        /// </summary>
        public ApplicationSection Section { get; set; }

        /// <summary>
        /// Section 1 fields - the only real data this form posts. The validation rules are on
        /// <see cref="ApplicantInformationViewModel"/>, written once and used both in the browser (jQuery unobtrusive)
        /// and on the server (ModelState). In the HTML the field names start with "ApplicantInformation.", which is how
        /// the controller validates just this section.
        /// </summary>
        public ApplicantInformationViewModel ApplicantInformation { get; set; } = new();

        /// <summary>Current status, shown in the page header. Display only.</summary>
        [BindNever] public ApplicationStatus Status { get; set; }

        /// <summary>"Property, unit N ($rent/month)" for the page header. Display only.</summary>
        [BindNever] public string UnitLabel { get; set; } = "";

        /// <summary>
        /// Section 2 rows, for display. Never bound from this form - residences only change through their modal, and
        /// that part of the page reloads from <c>ApplicationsController.Residences</c> after each save.
        /// </summary>
        [BindNever] public List<ResidenceViewModel> Residences { get; set; } = [];

        /// <summary>True once section 1 has been saved (and was valid) with Continue. One of the two things Submit needs (4.b.ii).</summary>
        [BindNever] public bool ApplicantInformationSaved { get; set; }

        /// <summary>True once section 2 has been saved with Continue (at least one residence). The other thing Submit needs.</summary>
        [BindNever] public bool ResidenceHistorySaved { get; set; }

        /// <summary>
        /// Decided on the server (4.d): only true for the applicant who owns it, and only while it's Draft or
        /// Returned. Property managers can look but never edit.
        /// </summary>
        [BindNever] public bool CanEdit { get; set; }

        /// <summary>
        /// What the section partials check to decide how to render. True when <see cref="CanEdit"/> is false, and
        /// always on the Summary (which is read-only no matter what, 4.a.iii).
        /// </summary>
        [BindNever] public bool IsReadOnly { get; set; }

        /// <summary>True for property managers - the page then shows the history panel and the Review button.</summary>
        [BindNever] public bool IsManager { get; set; }

        /// <summary>
        /// The reviewer's comment if it was returned or denied, so the applicant knows what to fix. That's the only
        /// comment applicants see - the full history is managers only (5.c).
        /// </summary>
        [BindNever] public string? ReviewComment { get; set; }

        /// <summary>
        /// Everything stopping Submit right now, from <see cref="SubmissionRules"/> (sections not saved, unit leased).
        /// The Summary lists these. Empty for anyone who can't edit.
        /// </summary>
        [BindNever] public List<string> SubmitBlockers { get; set; } = [];

        /// <summary>
        /// You can only submit once nothing is blocking it (4.b.ii, 4.e). Edit.cshtml greys out the button when this is
        /// false, and <c>ApplicationService.SubmitAsync</c> checks the same list on the server.
        /// </summary>
        public bool CanSubmit
        {
            get { return CanEdit && SubmitBlockers.Count == 0; }
        }

        /// <summary>
        /// Applicants can withdraw any time before it's final (Approved, Denied, Withdrawn; 5.b). Controls the
        /// Withdraw button - the controller and service check the same rule before doing anything.
        /// </summary>
        public bool CanWithdraw
        {
            get { return !IsManager && !ApplicationWorkflow.IsTerminal(Status); }
        }

        /// <summary>
        /// Managers can only review a Submitted application (5.a). Controls the Review button - the controller and
        /// service check the same rule before opening the modal or saving the review.
        /// </summary>
        public bool CanReview
        {
            get { return IsManager && ApplicationWorkflow.CanReview(Status); }
        }
    }
}
