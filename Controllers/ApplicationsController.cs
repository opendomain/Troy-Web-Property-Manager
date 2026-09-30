using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.Controllers
{
    /// <summary>Applicants see their own applications; property managers see all of them.</summary>
    [Authorize(Roles = AppRoles.Applicant + "," + AppRoles.PropertyManager)]
    public class ApplicationsController : AppController
    {
        public IActionResult Index() => View(CurrentUser);
    }
}
