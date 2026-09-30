using System.ComponentModel.DataAnnotations;

namespace Troy_Web_Property_Manager.ViewModels
{
    public class PropertyFormViewModel
    {
        public int? Id { get; set; }
        [Required, StringLength(50)] public string? Name { get; set; }
        [Required, StringLength(50)] public string? Address { get; set; }
    }
}
