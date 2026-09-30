using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Troy_Web_Property_Manager.Models
{
    public enum ApplicationStatus
    {
        Draft = 1,
        Submitted = 2,
        Returned = 3,
        Approved = 4,
        Denied = 5,
        Withdrawn = 6
    }
    public enum ReviewOutcome
    {
        Approve = 1,
        Return = 2,
        Deny = 3
    }

    /// <summary>The sections of the single-page application editor, in order.</summary>
    public enum ApplicationSection
    {
        [Display(Name = "Applicant information")] ApplicantInformation = 1,
        [Display(Name = "Residence history")] ResidenceHistory = 2,
        Summary = 3
    }

    public static class EnumExtensions
    {
        /// <summary>The value's [Display(Name)] if it has one, otherwise its name.</summary>
        public static string DisplayName(this Enum value) =>
            value.GetType().GetField(value.ToString())?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
    }
}
