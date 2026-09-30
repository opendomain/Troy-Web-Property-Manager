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
