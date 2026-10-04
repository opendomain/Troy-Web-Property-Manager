using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
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
        var sender = new EmailSender(Options.Create(new SendGridOptions()), logger, new TestEnvironment(environment));
        await Assert.ThrowsAsync<EmailSendException>(() => sender.SendEmailAsync("user@example.com", "Confirm", "secret-confirmation-token"));
        Assert.DoesNotContain(logger.Messages, m => m.Message.Contains("secret-confirmation-token"));
    }

    [Fact]
    public async Task MissingKey_InDevelopment_LogsEmailForLocalTesting()
    {
        var logger = new TestLogger<EmailSender>();
        var sender = new EmailSender(Options.Create(new SendGridOptions()), logger, new TestEnvironment("Development"));
        await sender.SendEmailAsync("user@example.com", "Confirm", "local-confirmation-token");
        Assert.Contains(logger.Messages, m => m.Message.Contains("local-confirmation-token"));
    }

    private sealed class TestEnvironment(string environment) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environment;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
