using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;

namespace Troy_Web_Property_Manager.Controllers
{
    /// <summary>Applicants see their own applications; property managers see all of them.</summary>
    [Authorize(Roles = AppRoles.Applicant + "," + AppRoles.PropertyManager)]
    public class ApplicationsController(ApplicationService applications) : AppController
    {
        public IActionResult Index() => View(CurrentUser);

        /// <summary>Apply for a unit from the Available units page.</summary>
        [HttpPost, Authorize(Roles = AppRoles.Applicant)]
        public async Task<IActionResult> Start(int unitId)
        {
            var result = await applications.StartAsync(unitId, CurrentUser);
            if (result.NotFound) return NotFound();
            if (!result.Succeeded)
            {
                SetError(result.Errors.Values.First());
                return RedirectToAction(nameof(UnitsController.Index), "Units");
            }

            // TODO: redirect to the application page once it exists.
            SetMessage($"Application #{result.Id} started.");
            return RedirectToAction(nameof(Index));
        }
    }
}
