using Microsoft.AspNetCore.Mvc.Rendering;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>The columns the Available units page can be sorted by (the header links send these as <c>sort=</c>).</summary>
    public enum UnitSortColumn
    {
        /// <summary>Property name, then unit number (the default).</summary>
        Property,
        Bedrooms,
        Rent,
        Type
    }

    /// <summary>
    /// The applicant's Available units page - the property and bedroom filters, the sort, and the units that match.
    /// Filters and sort are all in the query string (GET form and header links), so a sorted, filtered list can be
    /// bookmarked or refreshed, and <c>PropertyService.GetAvailableUnitsAsync</c> applies both in SQL.
    /// </summary>
    public class AvailableUnitsPageViewModel
    {
        public int? PropertyId { get; set; }

        /// <summary>Minimum number of bedrooms, or null for any.</summary>
        public int? Bedrooms { get; set; }

        public UnitSortColumn Sort { get; set; } = UnitSortColumn.Property;

        public SortDirection Dir { get; set; } = SortDirection.Asc;

        /// <summary>True if either filter is set, so the page can say "nothing matches" instead of "nothing available".</summary>
        public bool IsFiltered => PropertyId is not null || Bedrooms is not null;
        public List<SelectListItem> Properties { get; set; } = [];
        public List<AvailableUnitViewModel> Units { get; set; } = [];

        /// <summary>The direction a click on <paramref name="column"/>'s header asks for: flips the current sort, or
        /// starts ascending on a new column.</summary>
        public SortDirection NextDir(UnitSortColumn column)
        {
            return column == Sort && Dir == SortDirection.Asc ? SortDirection.Desc : SortDirection.Asc;
        }

        /// <summary>The header's <c>aria-sort</c> value, so screen readers know which column is sorted and which way.</summary>
        public string AriaSort(UnitSortColumn column)
        {
            return column != Sort ? "none" : Dir == SortDirection.Asc ? "ascending" : "descending";
        }

        /// <summary>The header's Font Awesome sort icon - the same icons the data grid uses.</summary>
        public string SortIcon(UnitSortColumn column)
        {
            return column != Sort ? "fa-sort data-grid-sort-idle" : Dir == SortDirection.Asc ? "fa-sort-up" : "fa-sort-down";
        }
    }
}
