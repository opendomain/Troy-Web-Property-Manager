using System.ComponentModel.DataAnnotations;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>
    /// The add/edit property modal (2.b). Same form for both - <see cref="Id"/> is null when adding.
    /// </summary>
    /// <remarks>
    /// We bind to this instead of the <c>Property</c> entity so only the fields on the form can be bound, and the
    /// validation messages stay out of the entity. Lengths match the nvarchar(50) columns.
    /// </remarks>
    public class PropertyFormViewModel
    {
        public int? Id { get; set; }
        [Required, StringLength(50)] public string? Name { get; set; }
        [Required, StringLength(50)] public string? Address { get; set; }
    }
}
