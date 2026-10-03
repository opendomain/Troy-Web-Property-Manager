namespace Troy_Web_Property_Manager.Services
{
    /// <summary>
    /// The current time, and the business's calendar. Timestamps (created, submitted, claimed, history, notes) are
    /// stored in UTC from <see cref="UtcNow"/>; "today" for the "is this unit leased today?" checks is the date in the
    /// business's time zone, not the server's, so moving the app to a server set to UTC doesn't make a lease start or
    /// end a day early.
    /// </summary>
    /// <remarks>
    /// <para>Why UTC for timestamps: a local wall-clock time is ambiguous for the hour the clocks go back each autumn
    /// (1:30 AM happens twice), so history rows stored in local time could sort out of order. UTC never repeats.</para>
    /// <para>Pages show timestamps in the business's zone through <see cref="ToBusinessTime(DateTime)"/>. The zone comes
    /// from the "BusinessTimeZone" setting (an IANA id like "America/New_York"); without one it falls back to the
    /// server's own zone.</para>
    /// </remarks>
    public class BusinessClock(TimeZoneInfo zone, TimeProvider? time = null)
    {
        private readonly TimeProvider _time = time ?? TimeProvider.System;

        /// <summary>The server's own zone - what services use when nothing is configured (e.g. in the tests).</summary>
        public static BusinessClock Local { get; } = new(TimeZoneInfo.Local);

        public TimeZoneInfo Zone { get; } = zone;

        /// <summary>Now, in UTC. This is what gets stored.</summary>
        public DateTime UtcNow
        {
            get { return _time.GetUtcNow().UtcDateTime; }
        }

        /// <summary>Today's date in the business's zone. Lease start and end dates are calendar dates on this calendar.</summary>
        public DateTime Today
        {
            get { return ToBusinessTime(UtcNow).Date; }
        }

        /// <summary>
        /// A stored UTC timestamp as the business's local time, with its offset, for showing on a page. EF reads
        /// <c>datetime</c> columns back with an unspecified kind, so the kind isn't trusted - stored times are UTC.
        /// </summary>
        public DateTimeOffset ToBusinessTime(DateTime utc)
        {
            return TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)), Zone);
        }

        /// <inheritdoc cref="ToBusinessTime(DateTime)"/>
        public DateTimeOffset? ToBusinessTime(DateTime? utc)
        {
            return utc is { } value ? ToBusinessTime(value) : null;
        }

        /// <summary>
        /// A wall-clock time in the business's zone as UTC, for storing. A time in the hour skipped when the clocks go
        /// forward doesn't exist, so it's moved an hour later; a time in the repeated hour in autumn is taken as the
        /// standard-time one.
        /// </summary>
        public DateTime ToUtc(DateTime businessTime)
        {
            var local = DateTime.SpecifyKind(businessTime, DateTimeKind.Unspecified);
            if (Zone.IsInvalidTime(local)) local = local.AddHours(1);
            return TimeZoneInfo.ConvertTimeToUtc(local, Zone);
        }

        /// <summary>Builds the clock from the "BusinessTimeZone" setting, or the server's zone if it isn't set.</summary>
        public static BusinessClock FromConfiguration(IConfiguration configuration)
        {
            var id = configuration["BusinessTimeZone"];
            return string.IsNullOrWhiteSpace(id) ? Local : new BusinessClock(TimeZoneInfo.FindSystemTimeZoneById(id));
        }
    }
}
