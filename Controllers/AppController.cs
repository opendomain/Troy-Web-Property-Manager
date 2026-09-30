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
        /// The modal form worked, but the current page no longer makes sense (e.g. you just left an application), so
        /// site.js closes the modal and goes to <paramref name="url"/> instead of refreshing.
        /// </summary>
        protected IActionResult ModalRedirect(string url)
        {
            return Json(new { success = true, redirectUrl = url });
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

        /// <summary>
        /// The service said no, so show its errors in the same partial. A conflict (someone else changed the data) goes
        /// back as 409 so site.js can tell it apart from a plain validation failure (422).
        /// </summary>
        protected IActionResult ModalFailed(string partialName, object model, ServiceResult result)
        {
            AddErrors(result);
            var response = PartialView(partialName, model);
            response.StatusCode = result.Conflict ? StatusCodes.Status409Conflict : StatusCodes.Status422UnprocessableEntity;
            return response;
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

        /// <summary>True if the result is a 404 or 403 rather than something to show on the form.</summary>
        protected static bool IsAccessFailure(ServiceResult result)
        {
            return result.NotFound || result.Forbidden;
        }

        /// <summary>Turns a not-found or forbidden result into the matching 404 / 403 response.</summary>
        protected IActionResult Failure(ServiceResult result)
        {
            return result.Forbidden ? Forbid() : NotFound();
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
