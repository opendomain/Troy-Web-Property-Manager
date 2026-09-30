using Microsoft.AspNetCore.Mvc.Rendering;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>The applicant's Available units page - the property filter and the units that match.</summary>
    public class AvailableUnitsPageViewModel
    {
        public int? PropertyId { get; set; }
        public List<SelectListItem> Properties { get; set; } = [];
        public List<AvailableUnitViewModel> Units { get; set; } = [];
    }
}
