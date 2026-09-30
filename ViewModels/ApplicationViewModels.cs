using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Rules;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>Section 1 fields. The validation rules for this section live here.</summary>
    public class ApplicantViewModel
    {
        // Lengths match the Applicant columns (nvarchar(50)).
        [Required, StringLength(50)] public string? Name { get; set; }
        [Required, Phone, StringLength(50)] public string? Phone { get; set; }
        [Required, EmailAddress, StringLength(50)] public string? Email { get; set; }
        [Required, StringLength(50), Display(Name = "Current address")] public string? CurrentAddress { get; set; }
    }

    /// <summary>One residence, edited in a modal.</summary>
    public class ResidenceViewModel : IValidatableObject
    {
        [BindNever] public int ApplicationId { get; set; }
        public int? ResidenceId { get; set; }
        // Lengths match the Residence columns (nvarchar(50)).
        [Required, StringLength(50)] public string? Address { get; set; }
        [Required, StringLength(50), Display(Name = "Landlord name")] public string? LandlordName { get; set; }
        [Required, Phone, StringLength(50), Display(Name = "Landlord phone")] public string? LandlordPhone { get; set; }
        [Required, DataType(DataType.Date), Display(Name = "Move-in date")] public DateOnly? MoveInDate { get; set; }
        [Required, DataType(DataType.Date), Display(Name = "Move-out date")] public DateOnly? MoveOutDate { get; set; }
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (MoveInDate is { } moveIn && MoveOutDate is { } moveOut && moveIn > moveOut)
            {
                yield return new ValidationResult("Move-out date must be on or after the move-in date.",
                [nameof(MoveOutDate)]);
            }
        }
    }

    /// <summary>
    /// The ONE view model behind the application page. Only Section and the Applicant Information
    /// fields are posted back; everything marked [BindNever] is display data rebuilt on the server.
    /// </summary>
    public class ApplicationEditorViewModel
    {
        public int Id { get; set; }
        public ApplicationSection Section { get; set; }
        public ApplicantViewModel Applicant { get; set; } = new();
        [BindNever] public ApplicationStatus Status { get; set; }
        [BindNever] public string UnitLabel { get; set; } = "";
        [BindNever] public List<ResidenceViewModel> Residences { get; set; } = [];
        [BindNever] public bool ApplicantSaved { get; set; }
        [BindNever] public bool ResidenceHistorySaved { get; set; }
        [BindNever] public bool CanEdit { get; set; } // server-side decision
        [BindNever] public bool IsReadOnly { get; set; } // !CanEdit, or on the Summary
        [BindNever] public bool IsManager { get; set; }
        public bool CanSubmit => CanEdit && ApplicantSaved && ResidenceHistorySaved;
        public bool CanWithdraw => !IsManager && !ApplicationWorkflow.IsTerminal(Status);
        public bool CanReview => IsManager && ApplicationWorkflow.CanReview(Status);
    }

    public class ReviewViewModel : IValidatableObject
    {
        [BindNever] public int ApplicationId { get; set; }
        [Required(ErrorMessage = "Choose an outcome.")] public ReviewOutcome? Outcome { get; set; }
        [StringLength(500)] public string? Comment { get; set; }
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Outcome is { } outcome && ApplicationWorkflow.RequiresComment(outcome) &&
            string.IsNullOrWhiteSpace(Comment))
            {
                yield return new ValidationResult("A comment is required to return or deny an application.",
                [nameof(Comment)]);
            }
        }
    }

    public class HistoryItemViewModel
    {
        public DateTime ChangedAt { get; set; }
        public string ChangedBy { get; set; } = "";
        /// <summary>Null for the first entry, when the application was started.</summary>
        public ApplicationStatus? FromStatus { get; set; }
        public ApplicationStatus ToStatus { get; set; }
        public ReviewOutcome? Outcome { get; set; }
        public string? Comment { get; set; }
    }

    public class ApplicationListItemViewModel
    {
        public int Id { get; set; }
        public string PropertyName { get; set; } = "";
        public string UnitNumber { get; set; } = "";
        public string Applicant { get; set; } = "";
        public ApplicationStatus Status { get; set; }
        public DateTime? SubmittedAt { get; set; }
    }

    public class ApplicationListViewModel
    {
        public ApplicationStatus? Status { get; set; }
        public int? PropertyId { get; set; }
        public List<ApplicationListItemViewModel> Items { get; set; } = [];
        public List<SelectListItem> Properties { get; set; } = [];
    }
}
