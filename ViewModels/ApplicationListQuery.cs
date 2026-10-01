using System.ComponentModel.DataAnnotations;
using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>The columns the application list can be sorted by. The names match the grid's column keys.</summary>
    public enum ApplicationSortColumn
    {
        /// <summary>Application number (the default, newest first).</summary>
        Id,
        /// <summary>Property name, then unit number.</summary>
        Property,
        /// <summary>The applicant's email.</summary>
        Applicant,
        /// <summary>Status name.</summary>
        Status,
        /// <summary>When it was (last) submitted. Never-submitted drafts have none and sort first ascending.</summary>
        Submitted
    }

    /// <summary>Sort direction.</summary>
    public enum SortDirection
    {
        Asc,
        Desc
    }

    /// <summary>
    /// What to show in the application list: the filters, the sort, and which page. It's bound from the query string
    /// by both the page and <c>GET /api/applications</c>, and <c>ApplicationService.ListAsync</c> turns all of it into
    /// SQL (WHERE, ORDER BY, OFFSET/FETCH), so only one page of rows ever leaves the database.
    /// </summary>
    public class ApplicationListQuery
    {
        public const int DefaultPageSize = 10;
        public const int MaxPageSize = 100;

        /// <summary>Only applications in this status. Leave it out for every status.</summary>
        public ApplicationStatus? Status { get; set; }

        /// <summary>Only applications for units in this property. Leave it out for every property.</summary>
        public int? PropertyId { get; set; }

        /// <summary>The column to sort by. Ties are broken by application number, so paging is stable.</summary>
        public ApplicationSortColumn Sort { get; set; } = ApplicationSortColumn.Id;

        /// <summary>Sort direction.</summary>
        public SortDirection Dir { get; set; } = SortDirection.Desc;

        /// <summary>The 1-based page. A page past the end returns the last page.</summary>
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        /// <summary>Rows per page, 1 to 100.</summary>
        [Range(1, MaxPageSize)]
        public int PageSize { get; set; } = DefaultPageSize;
    }
}
