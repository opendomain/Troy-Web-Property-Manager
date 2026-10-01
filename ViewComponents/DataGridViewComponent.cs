using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Troy_Web_Property_Manager.ViewModels;

namespace Troy_Web_Property_Manager.ViewComponents
{
    /// <summary>
    /// A sortable, paged table that loads its rows from a JSON endpoint. Use it as
    /// <c>&lt;vc:data-grid grid="..." /&gt;</c> with a <see cref="DataGridViewModel"/> saying where the rows come from
    /// and what the columns are.
    /// </summary>
    /// <remarks>
    /// <para>Unlike the other view components, this one doesn't load any data itself - the rows come from the endpoint,
    /// fetched by <c>wwwroot/js/grid.js</c>, and the endpoint does the sorting and paging (in SQL for the application
    /// list). The markup is in <c>Views/Shared/Components/DataGrid/Default.cshtml</c>: the table shell, the pager, and
    /// the grid's settings as JSON in a data- attribute for grid.js to read.</para>
    /// <para>So a new list only needs an endpoint that returns a <see cref="PagedResult{T}"/> and a column list - no
    /// new markup or JavaScript.</para>
    /// </remarks>
    public class DataGridViewComponent : ViewComponent
    {
        /// <summary>How the view writes the settings for grid.js: camelCase, with enums as camelCase names ("date").</summary>
        public static readonly JsonSerializerOptions SettingsJson = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        public IViewComponentResult Invoke(DataGridViewModel grid)
        {
            return View(grid);
        }
    }
}
