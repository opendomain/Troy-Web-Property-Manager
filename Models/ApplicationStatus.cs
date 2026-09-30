using System.ComponentModel.DataAnnotations;

namespace Troy_Web_Property_Manager.Models
{
    /// <summary>
    /// Application statuses (5.b). Approved, Denied and Withdrawn are final. See <c>Rules/ApplicationWorkflow</c> for
    /// which moves are allowed.
    /// </summary>
    /// <remarks>
    /// This is an enum (not just a lookup table) because the state machine cares about the specific values.
    /// They're saved as numbers and double as the ids in the Status lookup table, so don't renumber them -
    /// if we ever add a status, it goes on the end.
    /// </remarks>
    public enum ApplicationStatus : long
    {
        Draft = 1,
        Submitted = 2,
        Returned = 3,
        Approved = 4,
        Denied = 5,
        Withdrawn = 6,
        /// <summary>A property manager has claimed it from the review queue and is working on it.</summary>
        [Display(Name = "Under Review")]
        UnderReview = 7
    }
}
