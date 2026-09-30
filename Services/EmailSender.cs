using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Troy_Web_Property_Manager.Services
{
    /// <summary>Registered as a singleton so the one SendGridClient (and its HttpClient) is reused for every email.</summary>
    public class EmailSender : IEmailSender
    {
        private readonly SendGridOptions _options;
        private readonly ILogger<EmailSender> _logger;
        private readonly SendGridClient _client;

        public EmailSender(IOptions<SendGridOptions> options, ILogger<EmailSender> logger)
        {
            _options = options.Value;
            _logger = logger;
            _client = new SendGridClient(_options.ApiKey);
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // HTML only: the Identity messages are HTML, and sending them as the plain-text part would show raw tags.
            var msg = new SendGridMessage
            {
                From = new EmailAddress(_options.FromEmail, _options.FromName),
                Subject = subject,
                HtmlContent = htmlMessage
            };
            msg.AddTo(new EmailAddress(email));

            // SendGrid rewrites links for click tracking, which can break the confirmation token.
            msg.SetClickTracking(false, false);

            var response = await _client.SendEmailAsync(msg);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Body.ReadAsStringAsync();
                _logger.LogError("SendGrid failed to send to {Email}: {Status} {Body}", email, response.StatusCode, body);
                throw new InvalidOperationException("Failed to send email.");
            }
        }
    }
}
