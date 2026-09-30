using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Troy_Web_Property_Manager.Services
{
    /// <summary>
    /// Identity's <see cref="IEmailSender"/>, done with SendGrid. Identity uses it to send the confirmation email when
    /// someone registers (Program.cs turns on RequireConfirmedAccount). The API key comes from config (user secrets in
    /// development) - never put it in the code.
    /// It's a singleton so we reuse one SendGridClient (and its HttpClient) for every email.
    /// </summary>
    /// <remarks>
    /// With no API key configured (e.g. a fresh clone), there's no client. SendGridClient throws on an empty key, and
    /// since this is created by DI that would take down every page that injects it (Register included). Instead, in
    /// Development we log the email so you can still click the confirmation link; anywhere else we throw from
    /// <see cref="SendEmailAsync"/>, which Register catches and turns into a friendly message.
    /// </remarks>
    public class EmailSender : IEmailSender
    {
        private readonly SendGridOptions _options;
        private readonly ILogger<EmailSender> _logger;
        private readonly IHostEnvironment _environment;
        private readonly SendGridClient? _client;

        public EmailSender(IOptions<SendGridOptions> options, ILogger<EmailSender> logger, IHostEnvironment environment)
        {
            _options = options.Value;
            _logger = logger;
            _environment = environment;
            _client = string.IsNullOrWhiteSpace(_options.ApiKey) ? null : new SendGridClient(_options.ApiKey);
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (_client is null)
            {
                if (_environment.IsDevelopment())
                {
                    _logger.LogWarning("SendGrid isn't configured, so this email wasn't sent. To {Email}: {Subject}\n{Body}",
                        email, subject, htmlMessage);
                    return;
                }
                throw new InvalidOperationException("SendGrid isn't configured (SendGrid:ApiKey is missing).");
            }

            // HTML only. Identity's messages are HTML, so sending them as plain text would show the raw tags.
            var msg = new SendGridMessage
            {
                From = new EmailAddress(_options.FromEmail, _options.FromName),
                Subject = subject,
                HtmlContent = htmlMessage
            };
            msg.AddTo(new EmailAddress(email));

            // Turn off click tracking - SendGrid rewrites the links and that can break the confirmation token.
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
