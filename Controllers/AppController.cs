using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;

namespace Troy_Web_Property_Manager.Controllers
{
    /// <summary>Helpers shared by the MVC controllers.</summary>
    public abstract class AppController : Controller
    {
        protected CurrentUser CurrentUser
        {
            get { return new(User.FindFirstValue(ClaimTypes.NameIdentifier)!, User.IsInRole(AppRoles.PropertyManager)); }
        }

        /// <summary>Modal form succeeded: close it and refresh one region of the page (or reload the page when no target is given).</summary>
        protected IActionResult ModalSuccess(string? refreshTarget = null, string? refreshUrl = null)
        {
            return Json(new { success = true, refreshTarget, refreshUrl });
        }

        /// <summary>Modal form failed validation: return the SAME partial with errors so it re-renders in place.</summary>
        protected IActionResult ModalInvalid(string partialName, object model)
        {
            var result = PartialView(partialName, model);
            result.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return result;
        }

        /// <summary>Success message shown by _Layout on the next page render.</summary>
        protected void SetMessage(string message)
        {
            TempData["Message"] = message;
        }

        /// <summary>Error message shown by _Layout on the next page render.</summary>
        protected void SetError(string error)
        {
            TempData["Error"] = error;
        }

        protected void AddErrors(ServiceResult result, string prefix = "")
        {
            foreach (var (key, message) in result.Errors)
            {
                ModelState.AddModelError(key == "" ? "" : prefix + key, message);
            }
        }
    }
}
