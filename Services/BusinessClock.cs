namespace Troy_Web_Property_Manager.Services
{
    /// <summary>
    /// "Now" and "today" in the business's time zone, not the server's. Every timestamp we store (created, submitted,
    /// claimed, history) and every "is this unit leased today?" check goes through here, so moving the app to a
    /// server set to UTC doesn't shift the dates or make a lease start or end a day early.
    /// </summary>
    /// <remarks>
    /// The zone comes from the "BusinessTimeZone" setting (an IANA id like "America/New_York"); without one it falls
    /// back to the server's own zone, which is how the app behaved before. Stored times stay in that zone, so existing
    /// data doesn't need converting.
    /// </remarks>
    public class BusinessClock(TimeZoneInfo zone, TimeProvider? time = null)
    {
        private readonly TimeProvider _time = time ?? TimeProvider.System;

        /// <summary>The server's own zone - what services use when nothing is configured (e.g. in the tests).</summary>
        public static BusinessClock Local { get; } = new(TimeZoneInfo.Local);

        public TimeZoneInfo Zone { get; } = zone;

        public DateTime Now
        {
            get { return TimeZoneInfo.ConvertTime(_time.GetUtcNow(), Zone).DateTime; }
        }

        public DateTime Today
        {
            get { return Now.Date; }
        }

        /// <summary>Builds the clock from the "BusinessTimeZone" setting, or the server's zone if it isn't set.</summary>
        public static BusinessClock FromConfiguration(IConfiguration configuration)
        {
            var id = configuration["BusinessTimeZone"];
            return string.IsNullOrWhiteSpace(id) ? Local : new BusinessClock(TimeZoneInfo.FindSystemTimeZoneById(id));
        }
    }
}
