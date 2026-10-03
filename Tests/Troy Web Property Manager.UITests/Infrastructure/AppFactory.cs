using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Troy_Web_Property_Manager.UITests.Infrastructure
{
    /// <summary>
    /// Hosts the real app for the browser: the same Program.cs, served over Kestrel on a free local port (a browser
    /// can't reach the in-memory TestServer), in the Development environment so the demo data gets seeded.
    /// </summary>
    /// <remarks>
    /// <para>Three things are swapped for tests:</para>
    /// <list type="bullet">
    ///   <item>The database: a new LocalDB database per test run (<see cref="DatabaseName"/>), created, migrated and
    ///   seeded by Program's startup code, and dropped by <see cref="UiFixture"/> at the end. Your development
    ///   database is never touched - the fixture checks the connection before any test runs.</item>
    ///   <item>Email: <see cref="CapturingEmailSender"/> keeps the messages in memory, so registration tests can
    ///   follow the confirmation link, and nothing is ever sent through SendGrid (even with a key in user secrets).</item>
    ///   <item>SendGrid:ApiKey is set to <see cref="SendGridApiKey"/> - a dummy value by default. Register only calls
    ///   the email sender when a key is configured (with none it shows the link on the page instead), so the key has
    ///   to look set for the capturing sender to see anything. It replaces any real key from user secrets, so even if
    ///   something resolved the real sender, SendGrid would refuse it. Pass "" to run the app with no key at all
    ///   (see <see cref="NoSendGridKeyUiFixture"/>).</item>
    /// </list>
    /// </remarks>
    public sealed class AppFactory(string sendGridApiKey = "uitest-not-a-real-key") : WebApplicationFactory<Program>
    {
        public string SendGridApiKey { get; } = sendGridApiKey;

        public string DatabaseName { get; } = $"TroyWebPM_UITests_{Guid.NewGuid():N}";

        public string ConnectionString =>
            $"Server=(localdb)\\mssqllocaldb;Database={DatabaseName};Trusted_Connection=True;MultipleActiveResultSets=true";

        public CapturingEmailSender Emails { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
            builder.UseSetting("SendGrid:ApiKey", SendGridApiKey);
            // Test runs shouldn't count as someone running the app.
            builder.UseSetting("Telemetry:StartupPingUrl", "");
            builder.UseSetting("Telemetry:GoatCounterUrl", "");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(Emails);
            });
        }
    }
}
