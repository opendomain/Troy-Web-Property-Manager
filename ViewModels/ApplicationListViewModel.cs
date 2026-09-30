using Microsoft.AspNetCore.Mvc.Rendering;
using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.ViewModels
{
    public class ApplicationListViewModel
    {
        public ApplicationStatus? Status { get; set; }
        public int? PropertyId { get; set; }
        public List<ApplicationListItemViewModel> Items { get; set; } = [];
        public List<SelectListItem> Properties { get; set; } = [];
    }
}
