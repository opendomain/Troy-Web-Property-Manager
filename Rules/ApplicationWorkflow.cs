using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.Rules
{
    /// <summary>
    /// The application status state machine (5.b) and the rules that go with it.
    /// </summary>
    /// <remarks>
    /// <para>I pulled these out into a static class because they're plain functions - no database, no HTTP - so
    /// they're easy to unit test (<c>ApplicationWorkflowTests</c>). The service uses them to enforce things, the view
    /// model uses them to decide which buttons to show, and the seeder uses them to only build legal histories.
    /// Having one copy means the UI and the server can't disagree about what's allowed.</para>
    /// <para>There's a diagram in <c>Docs/Application Status State Diagram.png</c>.</para>
    /// </remarks>
    public static class ApplicationWorkflow
    {
        // Every allowed move. If it's not in here, it's not allowed. Approved, Denied and Withdrawn go nowhere -
        // they're final (5.b).
        private static readonly Dictionary<ApplicationStatus, ApplicationStatus[]> Allowed = new()
        {
            [ApplicationStatus.Draft] = [ApplicationStatus.Submitted, ApplicationStatus.Withdrawn],
            // Returned → Submitted is the "fix it and resubmit" path (Challenge a).
            [ApplicationStatus.Returned] = [ApplicationStatus.Submitted, ApplicationStatus.Withdrawn],
            // Submitted means it's waiting in the review queue. A manager has to claim it (Under Review) before
            // reviewing it, and the applicant can still pull out.
            [ApplicationStatus.Submitted] = [ApplicationStatus.UnderReview, ApplicationStatus.Withdrawn],
            // The three review outcomes (5.a), releasing it back to the queue, or the applicant pulling out.
            [ApplicationStatus.UnderReview] = [ApplicationStatus.Approved, ApplicationStatus.Returned, ApplicationStatus.Denied,
                ApplicationStatus.Submitted, ApplicationStatus.Withdrawn],
            [ApplicationStatus.Approved] = [],
            [ApplicationStatus.Denied] = [],
            [ApplicationStatus.Withdrawn] = []
        };

        /// <summary>Can we go from <paramref name="from"/> to <paramref name="to"/>?</summary>
        public static bool CanTransition(ApplicationStatus from, ApplicationStatus to)
        {
            return Allowed.TryGetValue(from, out var next) && next.Contains(to);
        }

        /// <summary>Approved, Denied and Withdrawn are final (5.b) - nothing else can happen after one of those.</summary>
        public static bool IsTerminal(ApplicationStatus status)
        {
            return status is ApplicationStatus.Approved or ApplicationStatus.Denied or ApplicationStatus.Withdrawn;
        }

        /// <summary>
        /// Applicants can only edit while it's Draft or Returned; otherwise everything is read-only (4.d). This one
        /// rule decides both how the page renders and whether the server accepts an edit.
        /// </summary>
        public static bool IsEditable(ApplicationStatus status)
        {
            return status is ApplicationStatus.Draft or ApplicationStatus.Returned;
        }

        /// <summary>
        /// Managers can only review an application they've claimed (5.a). This is the status half of the rule; the
        /// service also checks the claim is theirs.
        /// </summary>
        public static bool CanReview(ApplicationStatus status)
        {
            return status == ApplicationStatus.UnderReview;
        }

        /// <summary>Only a Submitted application is waiting in the review queue, so that's the only one a manager can claim.</summary>
        public static bool CanClaim(ApplicationStatus status)
        {
            return status == ApplicationStatus.Submitted;
        }

        /// <summary>A claimed (Under Review) application can be released back to the queue.</summary>
        public static bool CanRelease(ApplicationStatus status)
        {
            return status == ApplicationStatus.UnderReview;
        }

        /// <summary>The status a review outcome leads to. Throws on anything unexpected, since that'd be a bug.</summary>
        public static ApplicationStatus StatusFor(ReviewOutcome outcome)
        {
            return outcome switch
            {
                ReviewOutcome.Approve => ApplicationStatus.Approved,
                ReviewOutcome.Return => ApplicationStatus.Returned,
                ReviewOutcome.Deny => ApplicationStatus.Denied,
                _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown review outcome.")
            };
        }

        /// <summary>
        /// Return and Deny need a comment (5.a) so the applicant knows what to fix, or why. The review view model uses
        /// this for its validation message and the service uses it to enforce the rule.
        /// </summary>
        public static bool RequiresComment(ReviewOutcome outcome)
        {
            return outcome is ReviewOutcome.Return or ReviewOutcome.Deny;
        }

        /// <summary>Where Continue goes next (4.b.i): section 1 → section 2 → Summary.</summary>
        public static ApplicationSection Next(ApplicationSection section)
        {
            return section == ApplicationSection.ApplicantInformation ? ApplicationSection.ResidenceHistory : ApplicationSection.Summary;
        }

        /// <summary>Where Back goes (4.b.ii): Summary → section 2 → section 1.</summary>
        public static ApplicationSection Previous(ApplicationSection section)
        {
            return section == ApplicationSection.Summary ? ApplicationSection.ResidenceHistory : ApplicationSection.ApplicantInformation;
        }
    }
}
