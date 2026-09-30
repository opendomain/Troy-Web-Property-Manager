using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>One applicant in the application page's Applicants panel.</summary>
    public class ApplicationApplicantViewModel
    {
        public int ApplicantId { get; set; }

        /// <summary>Their login email (the applicant profile's email if the login is gone).</summary>
        public string Email { get; set; } = "";

        /// <summary>The applicant who started the application. They can't be removed.</summary>
        public bool IsStarter { get; set; }

        /// <summary>The signed-in user, so the panel can say "Leave" instead of "Remove".</summary>
        public bool IsYou { get; set; }
    }

    /// <summary>The "Add applicant" modal: the email of an existing applicant account.</summary>
    public class AddApplicantViewModel
    {
        [BindNever] public int ApplicationId { get; set; }

        /// <summary>256 matches the Identity email column.</summary>
        [Required, EmailAddress, StringLength(256)] public string? Email { get; set; }
    }
}
