using System.ComponentModel.DataAnnotations;

namespace Troy_Web_Property_Manager.Models
{
    /// <summary>
    /// The sections of the application page, in order (4.a) - two real sections and then the summary.
    /// Only one shows at a time. The current one rides along in the view model and comes back as a hidden field.
    /// [Display] gives each a friendly name (see EnumExtensions.DisplayName).
    /// </summary>
    public enum ApplicationSection : long
    {
        [Display(Name = "Applicant information")] ApplicantInformation = 1,
        [Display(Name = "Residence history")] ResidenceHistory = 2,
        Summary = 3
    }
}
