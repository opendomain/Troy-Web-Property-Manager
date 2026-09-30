using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Troy_Web_Property_Manager.ViewModels
{
    public class UnitFormViewModel
    {
        public int? Id { get; set; }
        public int PropertyId { get; set; }
        [Required, StringLength(20), Display(Name = "Unit number")] public string? UnitNumber { get; set; }
        [Required, Range(0, 10)] public int? Bedrooms { get; set; }
        [Required, Range(typeof(decimal), "1", "100000"), Display(Name = "Monthly rent")] public decimal? MonthlyRent { get; set; }
        [Required, Display(Name = "Unit type")] public int? UnitTypeId { get; set; }
        [BindNever] public List<SelectListItem> UnitTypes { get; set; } = [];
    }
}
