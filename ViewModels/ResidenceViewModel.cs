using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>
    /// One prior residence in section 2, Residence History (4.a.ii). Edited in the residence modal (4.c) via
    /// <c>_ResidenceForm.cshtml</c>, and also used for the read-only rows in the residence table.
    /// </summary>
    /// <remarks>
    /// <para>Single-field rules are DataAnnotations (checked in the browser and on the server). "Move-out has to be on
    /// or after move-in" involves two fields, so that one's done with <see cref="IValidatableObject"/>. MVC calls
    /// <see cref="Validate"/> once the attributes pass, and we hang the error on <see cref="MoveOutDate"/> so it shows
    /// under that field when the modal redraws.</para>
    /// <para><see cref="ApplicationId"/> is <c>[BindNever]</c> - the controller sets it from the route.
    /// <see cref="ResidenceId"/> does get posted (hidden field) so we know add from edit, but the service only looks
    /// for it in the current user's own application, so a faked id for someone else's residence finds nothing.</para>
    /// </remarks>
    public class ResidenceViewModel : IValidatableObject
    {
        [BindNever] public int ApplicationId { get; set; }

        /// <summary>Null when adding, the residence's id when editing.</summary>
        public int? ResidenceId { get; set; }

        // Lengths match the Residence columns (nvarchar(50)).
        [Required, StringLength(50)] public string? Address { get; set; }
        [Required, StringLength(50), Display(Name = "Landlord name")] public string? LandlordName { get; set; }
        [Required, Phone, StringLength(50), Display(Name = "Landlord phone")] public string? LandlordPhone { get; set; }

        // DateOnly since these are just calendar dates, no time. They're stored in "date" columns.
        [Required, DataType(DataType.Date), Display(Name = "Move-in date")] public DateOnly? MoveInDate { get; set; }
        [Required, DataType(DataType.Date), Display(Name = "Move-out date")] public DateOnly? MoveOutDate { get; set; }

        /// <summary>You can't move out before you moved in.</summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (MoveInDate is { } moveIn && MoveOutDate is { } moveOut && moveIn > moveOut)
            {
                yield return new ValidationResult("Move-out date must be on or after the move-in date.",
                [nameof(MoveOutDate)]);
            }
        }
    }
}
