namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>How a grid cell's value is formatted.</summary>
    public enum DataGridFormat
    {
        /// <summary>As-is.</summary>
        Text,
        /// <summary>A date/time from the server, shown as a date only (the date part as sent, no time zone shift).</summary>
        Date
    }

    /// <summary>One column of a data grid.</summary>
    public class DataGridColumn
    {
        /// <summary>The column's key. It's what goes in <c>sort=</c> when you sort by it, so it must be a value the
        /// endpoint's sort parameter accepts.</summary>
        public string Key { get; set; } = "";

        /// <summary>Header text.</summary>
        public string Title { get; set; } = "";

        /// <summary>The row property to show (camelCase, as it is in the JSON). Defaults to <see cref="Key"/>.</summary>
        public string? Field { get; set; }

        /// <summary>
        /// Text built from several row properties instead of one, like <c>"{propertyName} · {unitNumber}"</c>.
        /// Used instead of <see cref="Field"/> when set. The result is set as text, never HTML.
        /// </summary>
        public string? Template { get; set; }

        /// <summary>Makes the cell a link. Same <c>{field}</c> placeholders as <see cref="Template"/>, and each value is
        /// URL-encoded, e.g. <c>"/Applications/Edit/{id}"</c>.</summary>
        public string? Href { get; set; }

        public DataGridFormat Format { get; set; } = DataGridFormat.Text;

        /// <summary>Whether the header sorts by this column when clicked.</summary>
        public bool Sortable { get; set; } = true;
    }

    /// <summary>
    /// Everything the data grid view component needs: where to get rows, the columns, and the starting sort and page
    /// size. The component renders the table shell and grid.js fills it from <see cref="DataUrl"/>, which has to
    /// return a <see cref="PagedResult{T}"/> as JSON and accept <c>page</c>, <c>pageSize</c>, <c>sort</c> and
    /// <c>dir</c> (asc/desc) in the query string, plus whatever fields the filter form has.
    /// </summary>
    public class DataGridViewModel
    {
        /// <summary>The grid's element id. Must be unique on the page.</summary>
        public string Id { get; set; } = "data-grid";

        /// <summary>The JSON endpoint, without a query string.</summary>
        public string DataUrl { get; set; } = "";

        /// <summary>Short description of the table for screen readers (the table caption).</summary>
        public string Caption { get; set; } = "";

        public List<DataGridColumn> Columns { get; set; } = [];

        /// <summary>Column key to sort by until the user picks one.</summary>
        public string DefaultSort { get; set; } = "";

        /// <summary>"asc" or "desc".</summary>
        public string DefaultDir { get; set; } = "asc";

        /// <summary>Rows per page to start with. Should be one of <see cref="PageSizes"/>.</summary>
        public int PageSize { get; set; } = 10;

        /// <summary>The choices in the rows-per-page dropdown.</summary>
        public int[] PageSizes { get; set; } = [10, 25, 50, 100];

        /// <summary>
        /// Id of a GET form whose fields filter the grid. When set, the grid sends the form's fields with every request
        /// and submitting the form reloads the grid (back to page 1) instead of the page.
        /// </summary>
        public string? FilterFormId { get; set; }

        /// <summary>What to show when nothing matches.</summary>
        public string EmptyText { get; set; } = "Nothing found.";
    }
}
