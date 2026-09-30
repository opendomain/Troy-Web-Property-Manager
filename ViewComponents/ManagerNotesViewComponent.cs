using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;

namespace Troy_Web_Property_Manager.ViewComponents
{
    /// <summary>
    /// Property managers' private notes panel on the application page. Never rendered for applicants.
    /// </summary>
    /// <remarks>
    /// <para>It's a view component for the same reason as <see cref="ApplicationHistoryViewComponent"/>: it loads its
    /// own data, so the notes never go into <c>ApplicationEditorViewModel</c>, which is what applicants get.</para>
    /// <para>Managers only, checked here and again in <c>ApplicationService.GetManagerNotesAsync</c>. The markup is in
    /// <c>Views/Shared/Components/ManagerNotes/Default.cshtml</c>, which <c>ApplicationsController.ManagerNotes</c> also
    /// returns on its own to refresh the panel after the modal saves.</para>
    /// </remarks>
    public class ManagerNotesViewComponent(ApplicationService applications) : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync(int applicationId)
        {
            if (!UserClaimsPrincipal.IsInRole(AppRoles.PropertyManager)) return Content(string.Empty);

            var user = new CurrentUser(UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)!, IsManager: true);
            var model = await applications.GetManagerNotesAsync(applicationId, user);
            return model is null ? Content(string.Empty) : View(model);
        }
    }
}
