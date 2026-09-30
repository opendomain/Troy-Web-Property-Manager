using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Troy_Web_Property_Manager.ViewModels
{
    public class PropertyViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public List<UnitViewModel> Units { get; set; } = [];
    }

    public class UnitViewModel
    {
        public int Id { get; set; }
        public string UnitNumber { get; set; } = "";
        public int Bedrooms { get; set; }
        public decimal MonthlyRent { get; set; }
        public string UnitTypeName { get; set; } = "";
        public bool UnitTypeIsActive { get; set; }
        public bool IsLeased { get; set; }
    }

    public class PropertyFormViewModel
    {
        public int? Id { get; set; }
        [Required, StringLength(50)] public string? Name { get; set; }
        [Required, StringLength(50)] public string? Address { get; set; }
    }

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

    /// <summary>The applicant's Available units page: the property filter and the matching units.</summary>
    public class AvailableUnitsPageViewModel
    {
        public int? PropertyId { get; set; }
        public List<SelectListItem> Properties { get; set; } = [];
        public List<AvailableUnitViewModel> Units { get; set; } = [];
    }

    public class AvailableUnitViewModel
    {
        public int Id { get; set; }
        public string PropertyName { get; set; } = "";
        public string UnitNumber { get; set; } = "";
        public int Bedrooms { get; set; }
        public decimal MonthlyRent { get; set; }
        public string UnitTypeName { get; set; } = "";
    }
}
