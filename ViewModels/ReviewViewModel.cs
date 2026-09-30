using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Rules;

namespace Troy_Web_Property_Manager.ViewModels
{
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
}
