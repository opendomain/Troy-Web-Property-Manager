using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Troy_Web_Property_Manager.Services;

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
    ///   <item>The SendGrid settings: <see cref="SendGrid"/>, which starts with no API key, like a fresh clone. Register
    ///   then shows the confirmation link on the page instead of emailing it. Tests that want the email path set a
    ///   key with <see cref="UiFixture.SendGridConfigured"/>; it's never a real one, and it only reaches Register,
    ///   since the real sender is swapped out above.</item>
    /// </list>
    /// </remarks>
    public sealed class AppFactory : WebApplicationFactory<Program>
    {
        public string DatabaseName { get; } = $"TroyWebPM_UITests_{Guid.NewGuid():N}";

        public string ConnectionString =>
            $"Server=(localdb)\\mssqllocaldb;Database={DatabaseName};Trusted_Connection=True;MultipleActiveResultSets=true";

        public CapturingEmailSender Emails { get; } = new();

        /// <summary>The settings the app sees. Changes take effect on the next request.</summary>
        public SendGridOptions SendGrid { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
            builder.UseSetting("SendGrid:ApiKey", "");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(Emails);
                // Hand out our own instance instead of the one bound from config, so tests can change it while running.
                services.RemoveAll<IOptions<SendGridOptions>>();
                services.AddSingleton(Options.Create(SendGrid));
            });
        }
    }
}
