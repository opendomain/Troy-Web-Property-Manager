using Microsoft.Extensions.Configuration;
using Troy_Web_Property_Manager.Services;

namespace Troy_Web_Property_Manager.Tests.Services
{
    public class BusinessClockTests
    {
        /// <summary>A TimeProvider stuck at one instant.</summary>
        private sealed class FixedTime(DateTimeOffset utcNow) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow()
            {
                return utcNow;
            }
        }

        [Fact]
        public void Today_IsTheBusinessDate_NotTheUtcDate()
        {
            // 02:30 UTC on Jan 15 is still the evening of Jan 14 in New York (UTC-5 in winter).
            var clock = new BusinessClock(TimeZoneInfo.FindSystemTimeZoneById("America/New_York"),
                new FixedTime(new DateTimeOffset(2026, 1, 15, 2, 30, 0, TimeSpan.Zero)));

            Assert.Equal(new DateTime(2026, 1, 14, 21, 30, 0), clock.Now);
            Assert.Equal(new DateTime(2026, 1, 14), clock.Today);
        }

        [Fact]
        public void FromConfiguration_UsesTheConfiguredZone_OrFallsBackToTheServers()
        {
            var configured = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["BusinessTimeZone"] = "America/New_York" })
                .Build();
            var empty = new ConfigurationBuilder().Build();

            Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById("America/New_York"), BusinessClock.FromConfiguration(configured).Zone);
            Assert.Same(BusinessClock.Local, BusinessClock.FromConfiguration(empty));
        }
    }
}
