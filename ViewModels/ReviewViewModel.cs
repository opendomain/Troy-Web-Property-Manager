using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Rules;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>
    /// The manager's review modal (5.a): pick Approve, Return or Deny, plus a comment (required for Return and Deny).
    /// </summary>
    /// <remarks>
    /// <para>Whether the comment is required depends on the outcome, so it's an <see cref="IValidatableObject"/> rule.
    /// It asks <see cref="ApplicationWorkflow.RequiresComment"/> instead of hard-coding the outcomes, so the modal and
    /// the service can't disagree. If it fails, the controller sends this partial back with a 422 and the modal redraws
    /// with the message under Comment (Technical 1.b). <c>ApplicationService.ReviewAsync</c> checks again anyway, since
    /// someone could post straight to the server and skip this.</para>
    /// <para><see cref="ApplicationId"/> is <c>[BindNever]</c> - it always comes from the route, not the form.</para>
    /// </remarks>
    public class ReviewViewModel : IValidatableObject
    {
        [BindNever] public int ApplicationId { get; set; }

        /// <summary>Nullable so picking nothing gives a "Choose an outcome." error instead of quietly using a default.</summary>
        [Required(ErrorMessage = "Choose an outcome.")] public ReviewOutcome? Outcome { get; set; }

        /// <summary>Same 500-character limit as the history comment column.</summary>
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
