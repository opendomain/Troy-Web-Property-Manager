using System.Linq.Expressions;
using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.Rules
{
    public static class LeaseRules
    {
        public const int TermMonths = 12;

        /// <summary>End date for a lease starting on <paramref name="startDate"/>. The end date is exclusive.</summary>
        public static DateTime EndDateFor(DateTime startDate)
        {
            return startDate.Date.AddMonths(TermMonths);
        }

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
        public static bool IsActiveOn(Lease lease, DateTime day)
        {
            return ActiveOn(day).Compile()(lease);
        }
    }
}
