using System.Linq.Expressions;
using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.Rules
{
    /// <summary>
    /// Lease rules (2.d): approving an application creates a lease with a start date and a twelve-month term, and a
    /// unit with a lease covering today isn't available.
    /// </summary>
    /// <remarks>
    /// <see cref="ActiveOn"/> is an <see cref="Expression{TDelegate}"/> instead of a normal method so EF Core can turn
    /// it into SQL. That way "is this unit leased today?" runs in the database as part of bigger queries (available
    /// units, the property list, the submit and approval checks) instead of pulling leases into memory. And since it's
    /// defined once, all those queries use the same date logic.
    /// </remarks>
    public static class LeaseRules
    {
        /// <summary>Lease length from the assessment - twelve months.</summary>
        public const int TermMonths = 12;

        /// <summary>End date for a lease starting on <paramref name="startDate"/>. Note the end date is exclusive.</summary>
        public static DateTime EndDateFor(DateTime startDate)
        {
            return startDate.Date.AddMonths(TermMonths);
        }

        /// <summary>
        /// Is the lease active on this day? StartDate counts, EndDate doesn't, so a new lease can start the same day
        /// the old one ends. It's an expression so EF can turn it into SQL.
        /// </summary>
        public static Expression<Func<Lease, bool>> ActiveOn(DateTime day)
        {
            var date = day.Date;
            return lease => lease.StartDate <= date && lease.EndDate > date;
        }

        /// <summary>Same as <see cref="ActiveOn"/> but in memory, for code and tests that already have a lease object.</summary>
        public static bool IsActiveOn(Lease lease, DateTime day)
        {
            // Same comparison as ActiveOn, written out so we don't compile an expression on every call.
            var date = day.Date;
            return lease.StartDate <= date && lease.EndDate > date;
        }
    }
}
