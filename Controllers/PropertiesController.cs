using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.Controllers
{
    /// <summary>Property managers maintain properties and their units.</summary>
    [Authorize(Roles = AppRoles.PropertyManager)]
    public class PropertiesController : AppController
    {
        public IActionResult Index() => View();
    }
}
