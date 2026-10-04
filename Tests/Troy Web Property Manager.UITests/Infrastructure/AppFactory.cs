using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Troy_Web_Property_Manager.UITests.Infrastructure
{
    /// <summary>
    /// Hosts the real app for the browser: the same Program.cs, served over Kestrel on a free local port (a browser
    /// can't reach the in-memory TestServer). Defaults to Development; environment tests also exercise Production.
    /// </summary>
    /// <remarks>
    /// <para>Three things are swapped for tests:</para>
    /// <list type="bullet">
    ///   <item>The database: a new LocalDB database per test run (<see cref="DatabaseName"/>), created, migrated and
    ///   seeded by Program in Development or migrated by the Production fixture, and dropped by <see cref="UiFixture"/> at the end. Your development
    ///   database is never touched - the fixture checks the connection before any test runs.</item>
    ///   <item>Email: <see cref="CapturingEmailSender"/> keeps the messages in memory, so registration tests can
    ///   follow the confirmation link, and nothing is ever sent through SendGrid (even with a key in user secrets).</item>
    ///   <item>SendGrid:ApiKey is set to <see cref="SendGridApiKey"/> - a dummy value by default. Register only calls
    ///   the email sender when a key is configured (with none it shows a link only in Development), so the key has
    ///   to look set for the capturing sender to see anything. It replaces any real key from user secrets, so even if
    ///   something resolved the real sender, SendGrid would refuse it. Pass "" to run the app with no key at all
    ///   (see <see cref="NoSendGridKeyUiFixture"/>).</item>
    ///   <item>Registration:ShowConfirmationLink is set to <see cref="ShowConfirmationLink"/> - off by default, so
    ///   the Development confirmation page only shows the link when email fails (see <see cref="ShowConfirmationLinkUiFixture"/>
    ///   for the app with it on).</item>
    ///   <item>Telemetry: the startup ping and the GoatCounter page counter are off unless
    ///   <see cref="StartupPingUrl"/> or <see cref="GoatCounterUrl"/> is given (see <see cref="TelemetryUiFixture"/>,
    ///   whose ping goes to a <see cref="StartupPingCatcher"/> on this machine). Test runs shouldn't count as someone
    ///   using the app.</item>
    /// </list>
    /// </remarks>
    public sealed class AppFactory(string sendGridApiKey = "uitest-not-a-real-key", bool showConfirmationLink = false,
        string goatCounterUrl = "", string environment = "Development", string startupPingUrl = "")
        : WebApplicationFactory<Program>
    {
        public string SendGridApiKey { get; } = sendGridApiKey;

        public bool ShowConfirmationLink { get; } = showConfirmationLink;

        public string GoatCounterUrl { get; } = goatCounterUrl;

        public string StartupPingUrl { get; } = startupPingUrl;
        public string EnvironmentName { get; } = environment;

        public string DatabaseName { get; } = $"TroyWebPM_UITests_{Guid.NewGuid():N}";

        public string ConnectionString =>
            $"Server=(localdb)\\mssqllocaldb;Database={DatabaseName};Trusted_Connection=True;MultipleActiveResultSets=true";

        public CapturingEmailSender Emails { get; } = new();

        /// <summary>Everything the app logs, startup included.</summary>
        public CapturingLoggerProvider Logs { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(EnvironmentName);
            builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
            builder.UseSetting("SendGrid:ApiKey", SendGridApiKey);
            builder.UseSetting("Registration:ShowConfirmationLink", ShowConfirmationLink.ToString());
            // Test runs shouldn't count as someone running the app.
            builder.UseSetting("Telemetry:StartupPingUrl", StartupPingUrl);
            builder.UseSetting("Telemetry:GoatCounterUrl", GoatCounterUrl);
            builder.ConfigureLogging(logging =>
            {
                logging.AddProvider(Logs);
                // Program's own messages at every level (the failed startup ping is only a Debug message).
                logging.AddFilter<CapturingLoggerProvider>(typeof(Program).Assembly.GetName().Name, LogLevel.Debug);
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(Emails);
            });
        }
    }
}
