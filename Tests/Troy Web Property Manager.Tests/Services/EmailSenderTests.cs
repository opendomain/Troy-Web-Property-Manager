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

}
