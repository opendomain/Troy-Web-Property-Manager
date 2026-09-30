using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;

namespace Troy_Web_Property_Manager.ViewComponents
{
    /// <summary>History of status changes and review outcomes (who, when, comment). Shown to property managers only.</summary>
    public class ApplicationHistoryViewComponent(ApplicationService applications) : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync(int applicationId)
        {
            if (!UserClaimsPrincipal.IsInRole(AppRoles.PropertyManager)) return Content(string.Empty);

            var user = new CurrentUser(UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)!, IsManager: true);
            return View(await applications.GetHistoryAsync(applicationId, user));
        }
    }
}
