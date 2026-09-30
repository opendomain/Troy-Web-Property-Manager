using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;
using Troy_Web_Property_Manager.ViewModels;

namespace Troy_Web_Property_Manager.Controllers
{
    /// <summary>
    /// Applicants browse the available units here and apply for one (2.b). "Available" just means no lease covers
    /// today (2.d) - we work that out in SQL every time instead of storing it. The Apply button posts to
    /// <c>ApplicationsController.Start</c>. Applicants only; managers look after units on the Properties page.
    /// </summary>
    [Authorize(Roles = AppRoles.Applicant)]
    public class UnitsController(PropertyService properties) : AppController
    {
        /// <summary>
        /// The available units, filtered and sorted from the query string. An unknown <paramref name="sort"/> or
        /// <paramref name="dir"/> doesn't bind, so it just falls back to the default (property, ascending).
        /// </summary>
        public async Task<IActionResult> Index(int? propertyId, int? bedrooms, UnitSortColumn? sort, SortDirection? dir)
        {
            var model = new AvailableUnitsPageViewModel
            {
                PropertyId = propertyId,
                Bedrooms = bedrooms,
                Sort = sort ?? UnitSortColumn.Property,
                Dir = dir ?? SortDirection.Asc,
                Properties = await properties.GetPropertyOptionsAsync(propertyId)
            };
            model.Units = await properties.GetAvailableUnitsAsync(propertyId, bedrooms, model.Sort, model.Dir);
            return View(model);
        }
    }
}
