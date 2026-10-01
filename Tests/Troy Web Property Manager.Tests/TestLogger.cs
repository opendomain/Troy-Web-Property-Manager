using Microsoft.Extensions.Logging;

namespace Troy_Web_Property_Manager.Tests
{
    /// <summary>One message a service logged.</summary>
    public sealed record LoggedMessage(LogLevel Level, string Message);

    /// <summary>A logger that keeps what was logged, so tests can check what the services write.</summary>
    public sealed class TestLogger<T> : ILogger<T>
    {
        private readonly List<LoggedMessage> _messages = [];

        public IReadOnlyList<LoggedMessage> Messages
        {
            get { lock (_messages) return _messages.ToList(); }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_messages) _messages.Add(new LoggedMessage(logLevel, formatter(state, exception)));
        }

        /// <summary>True if something was logged at <paramref name="level"/> containing <paramref name="text"/>.</summary>
        public bool Has(LogLevel level, string text)
        {
            return Messages.Any(m => m.Level == level && m.Message.Contains(text));
        }
    }
}
