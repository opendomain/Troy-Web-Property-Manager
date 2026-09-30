using Microsoft.AspNetCore.Mvc.Rendering;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>The applicant's Available units page - the property and bedroom filters and the units that match.</summary>
    public class AvailableUnitsPageViewModel
    {
        public int? PropertyId { get; set; }

        /// <summary>Minimum number of bedrooms, or null for any.</summary>
        public int? Bedrooms { get; set; }

        /// <summary>True if either filter is set, so the page can say "nothing matches" instead of "nothing available".</summary>
        public bool IsFiltered => PropertyId is not null || Bedrooms is not null;
        public List<SelectListItem> Properties { get; set; } = [];
        public List<AvailableUnitViewModel> Units { get; set; } = [];
    }
}
