using System.ComponentModel.DataAnnotations;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>Section 1 fields. The validation rules for this section live here.</summary>
    public class ApplicantInformationViewModel
    {
        // Lengths match the Applicant columns (nvarchar(50)).
        [Required, StringLength(50)] public string? Name { get; set; }
        [Required, Phone, StringLength(50)] public string? Phone { get; set; }
        [Required, EmailAddress, StringLength(50)] public string? Email { get; set; }
        [Required, StringLength(50), Display(Name = "Current address")] public string? CurrentAddress { get; set; }
    }
}
