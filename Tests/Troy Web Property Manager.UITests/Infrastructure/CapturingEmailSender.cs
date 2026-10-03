using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity.UI.Services;
using Troy_Web_Property_Manager.Services;

namespace Troy_Web_Property_Manager.UITests.Infrastructure
{
    /// <summary>One email the app tried to send.</summary>
    public sealed record SentEmail(string To, string Subject, string HtmlBody);

    /// <summary>
    /// Stands in for the SendGrid sender: keeps every email in memory so a test can read it, for example to follow
    /// the account confirmation link after registering.
    /// </summary>
    public sealed partial class CapturingEmailSender : IEmailSender
    {
        private readonly ConcurrentQueue<SentEmail> _sent = new();
        private readonly ConcurrentDictionary<string, bool> _failFor = new(StringComparer.OrdinalIgnoreCase);

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (_failFor.ContainsKey(email)) throw new EmailSendException($"Simulated send failure to {email}.");
            _sent.Enqueue(new SentEmail(email, subject, htmlMessage));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Makes every send to <paramref name="to"/> throw, the way SendGrid does when it's down or refuses the
        /// message. Per address, so tests running against the shared app aren't affected.
        /// </summary>
        public void FailFor(string to)
        {
            _failFor[to] = true;
        }

        /// <summary>Every email sent to <paramref name="to"/>, oldest first.</summary>
        public IReadOnlyList<SentEmail> To(string to)
        {
            return _sent.Where(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>The link in the latest confirmation email to <paramref name="to"/>, decoded and ready to open.</summary>
        public string ConfirmationLink(string to)
        {
            var email = To(to).LastOrDefault(m => m.Subject.Contains("Confirm", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"No confirmation email was sent to {to}.");
            var match = HrefPattern().Match(email.HtmlBody);
            if (!match.Success) throw new InvalidOperationException($"The confirmation email to {to} has no link.");
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }

        [GeneratedRegex("href='([^']+)'")]
        private static partial Regex HrefPattern();
    }
}
