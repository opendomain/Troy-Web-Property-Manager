using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Troy_Web_Property_Manager.Services;

namespace Troy_Web_Property_Manager.Tests.Services;

public class EmailSenderTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task MissingKey_OutsideDevelopment_FailsWithoutLoggingConfirmationToken(string environment)
    {
        var logger = new TestLogger<EmailSender>();
        var sender = new EmailSender(Options.Create(new SendGridOptions()), logger, new TestWebHostEnvironment(environment));
        await Assert.ThrowsAsync<EmailSendException>(() => sender.SendEmailAsync("user@example.com", "Confirm", "secret-confirmation-token"));
        Assert.DoesNotContain(logger.Messages, m => m.Message.Contains("secret-confirmation-token"));
    }

    [Fact]
    public async Task MissingKey_InDevelopment_LogsEmailForLocalTesting()
    {
        var logger = new TestLogger<EmailSender>();
        var sender = new EmailSender(Options.Create(new SendGridOptions()), logger, new TestWebHostEnvironment("Development"));
        await sender.SendEmailAsync("user@example.com", "Confirm", "local-confirmation-token");
        Assert.Contains(logger.Messages, m => m.Message.Contains("local-confirmation-token"));
    }

    // ---------------- With a SendGrid key ----------------
    // SendGrid is never called for real: its requests go to a fake handler that answers like SendGrid would.

    private static SendGridOptions Configured(string? fromEmail = "noreply@example.com")
    {
        return new() { ApiKey = "SG.test-key", FromEmail = fromEmail, FromName = "Property Manager" };
    }

    /// <summary>Answers every request with <paramref name="respond"/> and keeps the request bodies.</summary>
    private sealed class FakeSendGrid(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return respond();
        }
    }

    private static EmailSender Sender(SendGridOptions options, TestLogger<EmailSender> logger, HttpMessageHandler handler)
    {
        return new(Options.Create(options), logger, new TestWebHostEnvironment("Production"), new HttpClient(handler));
    }

    [Fact]
    public async Task WithKey_SendsHtmlFromTheConfiguredSender_WithClickTrackingOff()
    {
        var logger = new TestLogger<EmailSender>();
        var sendGrid = new FakeSendGrid(() => new HttpResponseMessage(HttpStatusCode.Accepted));

        await Sender(Configured(), logger, sendGrid).SendEmailAsync("user@example.com", "Confirm", "<a href=\"x?a=1&amp;b=2\">Confirm</a>");

        using var json = JsonDocument.Parse(Assert.Single(sendGrid.Bodies));
        var root = json.RootElement;
        Assert.Equal("noreply@example.com", root.GetProperty("from").GetProperty("email").GetString());
        Assert.Equal("Property Manager", root.GetProperty("from").GetProperty("name").GetString());
        Assert.Equal("user@example.com", root.GetProperty("personalizations")[0].GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal("Confirm", root.GetProperty("subject").GetString());
        var content = Assert.Single(root.GetProperty("content").EnumerateArray());
        Assert.Equal("text/html", content.GetProperty("type").GetString());
        Assert.False(root.GetProperty("tracking_settings").GetProperty("click_tracking").GetProperty("enable").GetBoolean());
        Assert.True(logger.Has(LogLevel.Information, "Sent \"Confirm\" email to user@example.com."));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task WithKey_ButNoFromAddress_FailsBeforeCallingSendGrid(string? fromEmail)
    {
        var logger = new TestLogger<EmailSender>();
        var sendGrid = new FakeSendGrid(() => new HttpResponseMessage(HttpStatusCode.Accepted));

        await Assert.ThrowsAsync<EmailSendException>(() =>
            Sender(Configured(fromEmail), logger, sendGrid).SendEmailAsync("user@example.com", "Confirm", "body"));

        Assert.Empty(sendGrid.Bodies);
        Assert.True(logger.Has(LogLevel.Error, "SendGrid:FromEmail is not configured."));
    }

    [Fact]
    public async Task WithKey_AndTheRealConstructor_ChecksTheFromAddressFirst()
    {
        // The constructor DI uses builds a real SendGrid client; the missing sender stops it before any request.
        var logger = new TestLogger<EmailSender>();
        var sender = new EmailSender(Options.Create(Configured(fromEmail: "")), logger, new TestWebHostEnvironment("Production"));

        await Assert.ThrowsAsync<EmailSendException>(() => sender.SendEmailAsync("user@example.com", "Confirm", "body"));
    }

    [Fact]
    public async Task SendGridRefuses_FailsAndLogsItsAnswer()
    {
        var logger = new TestLogger<EmailSender>();
        var sendGrid = new FakeSendGrid(() => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"errors\":[{\"message\":\"bad key\"}]}")
        });

        await Assert.ThrowsAsync<EmailSendException>(() =>
            Sender(Configured(), logger, sendGrid).SendEmailAsync("user@example.com", "Confirm", "body"));

        Assert.True(logger.Has(LogLevel.Error, "SendGrid failed to send to user@example.com: Unauthorized"));
        Assert.True(logger.Has(LogLevel.Error, "bad key"));
    }

    [Fact]
    public async Task SendGridRefusesWithNoBody_StillFails()
    {
        var logger = new TestLogger<EmailSender>();
        var sendGrid = new FakeSendGrid(() => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = null });

        await Assert.ThrowsAsync<EmailSendException>(() =>
            Sender(Configured(), logger, sendGrid).SendEmailAsync("user@example.com", "Confirm", "body"));

        Assert.True(logger.Has(LogLevel.Error, "SendGrid failed to send to user@example.com: InternalServerError"));
    }

    [Fact]
    public async Task SendGridUnreachable_FailsWithTheCauseAttached()
    {
        var logger = new TestLogger<EmailSender>();
        var sendGrid = new FakeSendGrid(() => throw new HttpRequestException("No such host is known."));

        var ex = await Assert.ThrowsAsync<EmailSendException>(() =>
            Sender(Configured(), logger, sendGrid).SendEmailAsync("user@example.com", "Confirm", "body"));

        Assert.IsType<HttpRequestException>(ex.InnerException);
        Assert.True(logger.Has(LogLevel.Error, "SendGrid couldn't be reached to send to user@example.com."));
    }
}
