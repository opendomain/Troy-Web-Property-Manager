using System.ComponentModel.DataAnnotations;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>
    /// Section 1 of the application: Applicant Information (4.a.i). This is the one place its validation rules live.
    /// </summary>
    /// <remarks>
    /// <para>The same DataAnnotations work on both ends:</para>
    /// <list type="bullet">
    ///   <item>In the browser, the tag helpers write <c>data-val-*</c> attributes and jQuery unobtrusive validation
    ///   shows errors before anything is posted.</item>
    ///   <item>On the server, MVC validates into ModelState. <c>ApplicationsController.Edit</c> only looks at the
    ///   "ApplicantInformation." keys, so Continue only saves this section if it's valid (4.b.i).</item>
    /// </list>
    /// <para>The browser check is just a nicety - the server check is the one that matters, since anyone can turn
    /// off JavaScript.</para>
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
