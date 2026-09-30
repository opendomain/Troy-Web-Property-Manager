using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.Controllers
{
    /// <summary>Applicants browse available units and start an application for one.</summary>
    [Authorize(Roles = AppRoles.Applicant)]
    public class UnitsController : AppController
    {
        public IActionResult Index() => View();
    }
}
