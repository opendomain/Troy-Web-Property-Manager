using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Troy_Web_Property_Manager.ViewModels
{
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
}
