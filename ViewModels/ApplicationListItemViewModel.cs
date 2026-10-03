using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>
    /// One row in the application list (6.a). <c>ApplicationService.ListAsync</c> fills it with a <c>Select</c>, so
    /// SQL only sends back these columns instead of whole entities. <c>GET /api/applications</c> returns it as JSON.
    /// </summary>
    public class ApplicationListItemViewModel
    {
        /// <summary>Application number.</summary>
        public int Id { get; set; }

        /// <summary>Name of the property the unit is in.</summary>
        public string PropertyName { get; set; } = "";

        /// <summary>The unit applied for.</summary>
        public string UnitNumber { get; set; } = "";

        /// <summary>The applicant's email: the one on the application, or their profile's if it isn't saved yet.</summary>
        public string Applicant { get; set; } = "";

        /// <summary>Current status.</summary>
        public ApplicationStatus Status { get; set; }

        /// <summary>The status as it's shown on screen (e.g. "Under Review").</summary>
        public string StatusName
        {
            get { return Status.DisplayName(); }
        }

        /// <summary>
        /// When it was last submitted, in the business's time zone with its offset (e.g. 2026-10-03T14:05:00-04:00).
        /// Empty for a draft that was never submitted.
        /// </summary>
        public DateTimeOffset? SubmittedAt { get; set; }
    }
}
