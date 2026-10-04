using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;

namespace Troy_Web_Property_Manager.Tests.Controllers
{
    /// <summary>
    /// Just enough of a request for a controller action to run outside the web app: the signed-in user (as claims, the
    /// way the auth cookie provides them), URLs and TempData. Results are returned, not executed, so no views render.
    /// </summary>
    public static class ControllerTestContext
    {
        public static ClaimsPrincipal Principal(CurrentUser user)
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Role, user.IsManager ? AppRoles.PropertyManager : AppRoles.Applicant)
            ], authenticationType: "Test");
            return new ClaimsPrincipal(identity);
        }

        public static T SignedInAs<T>(this T controller, CurrentUser user) where T : Controller
        {
            var http = new DefaultHttpContext { User = Principal(user) };
            controller.ControllerContext = new ControllerContext { HttpContext = http };
            controller.Url = new FakeUrlHelper(controller.ControllerContext);
            controller.TempData = new TempDataDictionary(http, new NoTempDataProvider());
            return controller;
        }

        /// <summary>Builds "/Controller/Action" URLs; the tests only check that there is one.</summary>
        public sealed class FakeUrlHelper(ActionContext context) : IUrlHelper
        {
            public ActionContext ActionContext { get; } = context;

            public string? Action(UrlActionContext actionContext)
            {
                return $"/{actionContext.Controller ?? "Current"}/{actionContext.Action}";
            }

            public string? Content(string? contentPath)
            {
                return contentPath;
            }

            public bool IsLocalUrl(string? url)
            {
                return true;
            }

            public string? Link(string? routeName, object? values)
            {
                return routeName;
            }

            public string? RouteUrl(UrlRouteContext routeContext)
            {
                // Url.Page ends up here.
                return routeContext.RouteName ?? "/page";
            }
        }

        public sealed class NoTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context)
            {
                return new Dictionary<string, object>();
            }

            public void SaveTempData(HttpContext context, IDictionary<string, object> values)
            {
            }
        }
    }
}
