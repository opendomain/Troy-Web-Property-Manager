using System.Linq.Expressions;
using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.Rules
{
    public static class UnitTypeRules
    {
        /// <summary>An inactive type is allowed only if it is already the unit's current type (currentUnitTypeId is null for a new unit).</summary>
        public static bool CanAssign(UnitType type, int? currentUnitTypeId) => type.Active || type.Id == currentUnitTypeId;
    }

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
            section == ApplicationSection.Applicant ? ApplicationSection.ResidenceHistory :
            ApplicationSection.Summary;
        public static ApplicationSection Previous(ApplicationSection section) =>
            section == ApplicationSection.Summary ? ApplicationSection.ResidenceHistory :
            ApplicationSection.Applicant;
    }

    public static class LeaseRules
    {
        public const int TermMonths = 12;

        /// <summary>End date for a lease starting on <paramref name="startDate"/>. The end date is exclusive.</summary>
        public static DateTime EndDateFor(DateTime startDate) => startDate.Date.AddMonths(TermMonths);

        /// <summary>
        /// A lease is active on a day when its term covers that day: StartDate is inclusive, EndDate is exclusive,
        /// so a new lease can start on the day the previous one ends. Written as an expression so EF translates it to SQL.
        /// </summary>
        public static Expression<Func<Lease, bool>> ActiveOn(DateTime day)
        {
            var date = day.Date;
            return lease => lease.StartDate <= date && lease.EndDate > date;
        }

        /// <summary>In-memory form of <see cref="ActiveOn"/>, for code and tests that already have a lease.</summary>
        public static bool IsActiveOn(Lease lease, DateTime day) => ActiveOn(day).Compile()(lease);
    }
}
