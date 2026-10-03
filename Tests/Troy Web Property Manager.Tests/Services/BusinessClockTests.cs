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

        private static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        [Fact]
        public void UtcNow_IsUtc_AndToday_IsTheBusinessDate()
        {
            // 02:30 UTC on Jan 15 is still the evening of Jan 14 in New York (UTC-5 in winter).
            var clock = new BusinessClock(NewYork, new FixedTime(new DateTimeOffset(2026, 1, 15, 2, 30, 0, TimeSpan.Zero)));

            Assert.Equal(new DateTime(2026, 1, 15, 2, 30, 0, DateTimeKind.Utc), clock.UtcNow);
            Assert.Equal(DateTimeKind.Utc, clock.UtcNow.Kind);
            Assert.Equal(new DateTime(2026, 1, 14), clock.Today);
        }

        [Fact]
        public void ToBusinessTime_ShowsAStoredUtcTime_InTheBusinessZone_WithItsOffset()
        {
            var clock = new BusinessClock(NewYork);
            // EF hands datetime columns back with an unspecified kind; they're still UTC.
            var stored = new DateTime(2026, 7, 4, 16, 0, 0, DateTimeKind.Unspecified);

            Assert.Equal(new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.FromHours(-4)), clock.ToBusinessTime(stored));
            Assert.Null(clock.ToBusinessTime((DateTime?)null));
        }

        [Fact]
        public void TheHourRepeatedInAutumn_IsTwoDifferentUtcTimes_InOrder()
        {
            // Nov 1 2026: New York goes from 2:00 EDT back to 1:00 EST, so 1:30 AM local happens twice. Stored in UTC the
            // two are an hour apart and sort correctly; on the local wall clock they'd be the same value.
            var clock = new BusinessClock(NewYork);
            var first = new DateTime(2026, 11, 1, 5, 30, 0, DateTimeKind.Utc);   // 1:30 EDT
            var second = new DateTime(2026, 11, 1, 6, 30, 0, DateTimeKind.Utc);  // 1:30 EST

            Assert.Equal(clock.ToBusinessTime(first).DateTime, clock.ToBusinessTime(second).DateTime);
            Assert.True(clock.ToBusinessTime(first) < clock.ToBusinessTime(second));
        }

        [Fact]
        public void ToUtc_ConvertsLocalTimes_AndMovesTheSkippedSpringHourForward()
        {
            var clock = new BusinessClock(NewYork);

            Assert.Equal(new DateTime(2026, 1, 14, 21, 30, 0).AddHours(5), clock.ToUtc(new DateTime(2026, 1, 14, 21, 30, 0)));
            // Mar 8 2026: 2:00 AM jumps to 3:00 AM, so 2:30 doesn't exist. It's taken as 3:30 EDT (07:30 UTC).
            Assert.Equal(new DateTime(2026, 3, 8, 7, 30, 0), clock.ToUtc(new DateTime(2026, 3, 8, 2, 30, 0)));
        }

        [Fact]
        public void FromConfiguration_UsesTheConfiguredZone_OrFallsBackToTheServers()
        {
            var configured = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["BusinessTimeZone"] = "America/New_York" })
                .Build();
            var empty = new ConfigurationBuilder().Build();

            Assert.Equal(NewYork, BusinessClock.FromConfiguration(configured).Zone);
            Assert.Same(BusinessClock.Local, BusinessClock.FromConfiguration(empty));
        }
    }
}
