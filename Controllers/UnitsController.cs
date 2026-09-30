using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;
using Troy_Web_Property_Manager.ViewModels;

namespace Troy_Web_Property_Manager.Controllers
{
    /// <summary>Applicants browse available units and start an application for one.</summary>
    [Authorize(Roles = AppRoles.Applicant)]
    public class UnitsController(PropertyService properties) : AppController
    {
        public async Task<IActionResult> Index(int? propertyId) => View(new AvailableUnitsPageViewModel
        {
            PropertyId = propertyId,
            Properties = await properties.GetPropertyOptionsAsync(propertyId),
            Units = await properties.GetAvailableUnitsAsync(propertyId)
        });
    }
}
