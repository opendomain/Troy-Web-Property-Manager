using Microsoft.AspNetCore.Mvc.Rendering;
using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>
    /// The application list page (6.a): the current filters, the property dropdown, and the rows that match.
    /// The filters are in the query string (it's a GET form), so you can bookmark or refresh a filtered list.
    /// <c>ApplicationService.ListAsync</c> applies them in SQL, not in memory.
    /// </summary>
    public class ApplicationListViewModel
    {
        public ApplicationStatus? Status { get; set; }
        public int? PropertyId { get; set; }
        public List<ApplicationListItemViewModel> Items { get; set; } = [];
        public List<SelectListItem> Properties { get; set; } = [];
    }
}
