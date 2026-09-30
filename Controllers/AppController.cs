using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;

namespace Troy_Web_Property_Manager.Controllers
{
    /// <summary>
    /// Base class for our controllers. Keeps the modal helpers and error handling in one spot so every controller
    /// does it the same way.
    /// </summary>
    public abstract class AppController : Controller
    {
        /// <summary>
        /// Who's signed in: their Identity user id and whether they're a property manager.
        /// We hand this to the services instead of letting them dig into HttpContext, which keeps them easy to test.
        /// </summary>
        protected CurrentUser CurrentUser
        {
            get { return new(User.FindFirstValue(ClaimTypes.NameIdentifier)!, User.IsInRole(AppRoles.PropertyManager)); }
        }

        /// <summary>
        /// The modal form worked. site.js closes the modal and reloads one part of the page from refreshUrl
        /// (or the whole page if there's no target).
        /// </summary>
        protected IActionResult ModalSuccess(string? refreshTarget = null, string? refreshUrl = null)
        {
            return Json(new { success = true, refreshTarget, refreshUrl });
        }

        /// <summary>
        /// The modal form didn't validate, so send the same partial back with the errors and the modal redraws in place.
        /// We use 422 so site.js can tell this apart from a success (JSON) or an actual error.
        /// </summary>
        protected IActionResult ModalInvalid(string partialName, object model)
        {
            var result = PartialView(partialName, model);
            result.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return result;
        }

        /// <summary>Green message that _Layout shows on the next page.</summary>
        protected void SetMessage(string message)
        {
            TempData["Message"] = message;
        }

        /// <summary>Red message that _Layout shows on the next page.</summary>
        protected void SetError(string error)
        {
            TempData["Error"] = error;
        }

        /// <summary>
        /// Puts service errors into ModelState so they show up like normal validation errors.
        /// </summary>
        protected void AddErrors(ServiceResult result, string prefix = "")
        {
            foreach (var (key, message) in result.Errors)
            {
                ModelState.AddModelError(key == "" ? "" : prefix + key, message);
            }
        }
    }
}
