namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>
    /// The review queue page (property managers only). <c>ApplicationService.GetQueueAsync</c> fills all three lists
    /// from one query, oldest submission first.
    /// </summary>
    public class ReviewQueueViewModel
    {
        /// <summary>Under Review and claimed by the signed-in manager - their work in progress.</summary>
        public List<ReviewQueueItemViewModel> Mine { get; set; } = [];

        /// <summary>Submitted and not claimed by anyone yet. The first one has been waiting the longest.</summary>
        public List<ReviewQueueItemViewModel> Waiting { get; set; } = [];

        /// <summary>Under Review by other managers. Shown so nobody doubles up, and so a stuck claim can be released.</summary>
        public List<ReviewQueueItemViewModel> ClaimedByOthers { get; set; } = [];
    }

    /// <summary>One row on the review queue page.</summary>
    public class ReviewQueueItemViewModel
    {
        public int Id { get; set; }
        public string PropertyName { get; set; } = "";
        public string UnitNumber { get; set; } = "";
        public string Applicant { get; set; } = "";
        public DateTime? SubmittedAt { get; set; }

        /// <summary>Email of the manager who claimed it. Null while it's waiting.</summary>
        public string? Reviewer { get; set; }

        public DateTime? ClaimedAt { get; set; }
    }
}
