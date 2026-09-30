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
        public async Task<IActionResult> Index(int? propertyId, int? bedrooms)
        {
            return View(new AvailableUnitsPageViewModel
            {
                PropertyId = propertyId,
                Bedrooms = bedrooms,
                Properties = await properties.GetPropertyOptionsAsync(propertyId),
                Units = await properties.GetAvailableUnitsAsync(propertyId, bedrooms)
            });
        }
    }
}
