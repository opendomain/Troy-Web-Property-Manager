using System.ComponentModel.DataAnnotations;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>
    /// Section 1 of the application: Applicant Information (4.a.i). This is the one place its validation rules live.
    /// </summary>
    /// <remarks>
    /// <para>The same DataAnnotations work everywhere:</para>
    /// <list type="bullet">
    ///   <item>In the browser, the tag helpers write <c>data-val-*</c> attributes and jQuery unobtrusive validation
    ///   shows errors as you type. It doesn't stop the post (Continue has <c>formnovalidate</c>).</item>
    ///   <item>On the server, <c>SectionValidator</c> runs them against the saved section. Continue saves even when
    ///   they fail (except text longer than the column), then shows the errors on their fields, and the Summary
    ///   lists them as blockers until they're fixed.</item>
    /// </list>
    /// <para>The properties are nullable with an explicit <c>[Required]</c> (and <c>SuppressImplicitRequiredAttributeFor…</c>
    /// is on in Program.cs) so a blank field gets a friendly "required" message instead of a binding error.</para>
    /// </remarks>
    public class ApplicantInformationViewModel
    {
        // Lengths match the nvarchar(50) columns, so anything that passes validation will fit.
        [Required, StringLength(50)] public string? Name { get; set; }
        [Required, Phone, StringLength(50)] public string? Phone { get; set; }
        [Required, EmailAddress, StringLength(50)] public string? Email { get; set; }
        [Required, StringLength(50), Display(Name = "Current address")] public string? CurrentAddress { get; set; }
    }
}
