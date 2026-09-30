using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;
using Troy_Web_Property_Manager.ViewModels;

namespace Troy_Web_Property_Manager.Controllers.Api
{
    /// <summary>
    /// JSON for the application list grid. The page's data grid (grid.js) calls it every time you sort, page or filter.
    /// </summary>
    /// <remarks>
    /// <para>Same security as the page: the class is limited to our two roles, and <c>ApplicationService.ListAsync</c>
    /// starts from its <c>Visible</c> query, so applicants only ever get their own applications and managers only ones
    /// that have been submitted. It uses the same auth cookie as the rest of the site; an API call that isn't signed
    /// in gets a 401 rather than the login redirect (see Program.cs).</para>
    /// <para>[ApiController] gives us the automatic 400 (validation problem details) when the query string doesn't
    /// bind - an unknown sort column, a page size over 100 - so the action only ever sees a valid query. It's
    /// documented in the OpenAPI document at <c>/openapi/v1.json</c> (Development only).</para>
    /// </remarks>
    [ApiController]
    [Route("api/applications")]
    [Produces("application/json")]
    [Authorize(Roles = AppRoles.Applicant + "," + AppRoles.PropertyManager)]
    public class ApplicationsApiController(ApplicationService applications) : ControllerBase
    {
        /// <summary>List applications, one page at a time.</summary>
        /// <remarks>
        /// Filters, sorts and pages in the database and returns one page of rows plus the number of applications that
        /// match the filters. Applicants see the applications they're on; property managers see every application that
        /// has been submitted at least once. A page past the end returns the last page, and <c>page</c> in the
        /// response says which page it is.
        /// </remarks>
        /// <param name="query">Filters, sort and page.</param>
        /// <response code="200">The page of rows and the filtered total.</response>
        /// <response code="400">A query string value is out of range or not recognised.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Signed in, but neither an applicant nor a property manager.</response>
        [HttpGet(Name = "ListApplications")]
        [ProducesResponseType<PagedResult<ApplicationListItemViewModel>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<PagedResult<ApplicationListItemViewModel>>> List([FromQuery] ApplicationListQuery query)
        {
            return await applications.ListAsync(query, CurrentUser.From(User));
        }
    }
}
