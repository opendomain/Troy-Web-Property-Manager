using System.ComponentModel.DataAnnotations;

namespace Troy_Web_Property_Manager.Models
{
    /// <summary>The sections of the single-page application editor, in order.</summary>
    public enum ApplicationSection : long
    {
        [Display(Name = "Applicant information")] ApplicantInformation = 1,
        [Display(Name = "Residence history")] ResidenceHistory = 2,
        Summary = 3
    }
}
