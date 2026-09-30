namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>
    /// One page of a list, plus how many rows matched the filters in total, so a grid can draw its pager.
    /// This is what the JSON list endpoints return and what the data grid component (grid.js) reads.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="Items">The rows on this page, already sorted.</param>
    /// <param name="Total">How many rows match the filters across every page (not just this one).</param>
    /// <param name="Page">The 1-based page these rows are from. Can be lower than the one asked for: a page past the
    /// end comes back as the last page.</param>
    /// <param name="PageSize">The page size that was used.</param>
    public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
}
