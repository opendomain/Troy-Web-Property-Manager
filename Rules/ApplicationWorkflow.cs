using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.Rules
{
    public static class ApplicationWorkflow
    {
        private static readonly Dictionary<ApplicationStatus, ApplicationStatus[]> Allowed = new()
        {
            [ApplicationStatus.Draft] = [ApplicationStatus.Submitted, ApplicationStatus.Withdrawn],
            [ApplicationStatus.Returned] = [ApplicationStatus.Submitted, ApplicationStatus.Withdrawn],
            [ApplicationStatus.Submitted] = [ApplicationStatus.Approved, ApplicationStatus.Returned, ApplicationStatus.Denied, ApplicationStatus.Withdrawn],
            [ApplicationStatus.Approved] = [],
            [ApplicationStatus.Denied] = [],
            [ApplicationStatus.Withdrawn] = []
        };

        public static bool CanTransition(ApplicationStatus from, ApplicationStatus to) =>
            Allowed.TryGetValue(from, out var next) && next.Contains(to);

        public static bool IsTerminal(ApplicationStatus status) => status is ApplicationStatus.Approved or ApplicationStatus.Denied or ApplicationStatus.Withdrawn;

        /// <summary>The applicant may edit only while Draft or Returned; otherwise every section is read-only.</summary>
        public static bool IsEditable(ApplicationStatus status) => status is ApplicationStatus.Draft or ApplicationStatus.Returned;
        
        public static bool CanReview(ApplicationStatus status) => status == ApplicationStatus.Submitted;
        
        public static ApplicationStatus StatusFor(ReviewOutcome outcome) => outcome switch
        {
            ReviewOutcome.Approve => ApplicationStatus.Approved,
            ReviewOutcome.Return => ApplicationStatus.Returned,
            ReviewOutcome.Deny => ApplicationStatus.Denied,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown review outcome.")
        };

        public static bool RequiresComment(ReviewOutcome outcome) => outcome is ReviewOutcome.Return or ReviewOutcome.Deny;

        public static ApplicationSection Next(ApplicationSection section) =>
            section == ApplicationSection.ApplicantInformation ? ApplicationSection.ResidenceHistory : ApplicationSection.Summary;
        
        public static ApplicationSection Previous(ApplicationSection section) =>
            section == ApplicationSection.Summary ? ApplicationSection.ResidenceHistory : ApplicationSection.ApplicantInformation;
    }
}
