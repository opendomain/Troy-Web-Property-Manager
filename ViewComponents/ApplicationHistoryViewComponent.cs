using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;

namespace Troy_Web_Property_Manager.ViewComponents
{
    /// <summary>
    /// The history of status changes and reviews (who, when, comment). Property managers only (5.c).
    /// </summary>
    /// <remarks>
    /// <para>Why a view component instead of a partial? The assessment wants both (Technical 1.a), and this panel is a
    /// good fit: it needs its own data (history rows joined to user emails) that the page's view model doesn't have.
    /// A partial can only show what it's given, but a view component has its own <see cref="InvokeAsync"/> with DI and
    /// can go get the data itself. The editor page doesn't need to know how the history is loaded - it just drops in
    /// <c>&lt;vc:application-history&gt;</c> (the <c>@addTagHelper</c> in Views/_ViewImports.cshtml turns that on).</para>
    /// <para>It's managers only, and we check that twice: here (an applicant gets nothing even if a view calls it for
    /// them) and in <c>ApplicationService.GetHistoryAsync</c>, which returns nothing for non-managers.</para>
    /// <para>The markup is in the usual spot, <c>Views/Shared/Components/ApplicationHistory/Default.cshtml</c>.</para>
    /// </remarks>
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
