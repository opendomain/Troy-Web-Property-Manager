using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Rules;

namespace Troy_Web_Property_Manager.Tests.Rules
{
    public class LeaseRulesTests
    {
        [Fact]
        public void EndDateFor_IsTwelveMonthsLaterAtMidnight()
        {
            var end = LeaseRules.EndDateFor(new DateTime(2026, 3, 15, 14, 30, 0));
            Assert.Equal(new DateTime(2027, 3, 15), end);
        }

        [Fact]
        public void EndDateFor_ClampsToEndOfShorterMonth()
        {
            Assert.Equal(new DateTime(2025, 2, 28), LeaseRules.EndDateFor(new DateTime(2024, 2, 29)));
        }

        [Theory]
        [InlineData("2026-01-01", true)]  // start date is inclusive
        [InlineData("2026-06-15", true)]
        [InlineData("2026-12-31", true)]
        [InlineData("2027-01-01", false)] // end date is exclusive, so the next lease can start that day
        [InlineData("2025-12-31", false)] // before the lease starts
        public void IsActiveOn_StartInclusiveEndExclusive(string day, bool expected)
        {
            var lease = new Lease { StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2027, 1, 1) };
            Assert.Equal(expected, LeaseRules.IsActiveOn(lease, DateTime.Parse(day)));
        }

        [Fact]
        public void IsActiveOn_IgnoresTimeOfDay()
        {
            var lease = new Lease { StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2027, 1, 1) };
            Assert.False(LeaseRules.IsActiveOn(lease, new DateTime(2027, 1, 1, 9, 0, 0)));
            Assert.True(LeaseRules.IsActiveOn(lease, new DateTime(2026, 12, 31, 23, 59, 0)));
        }
    }
}
