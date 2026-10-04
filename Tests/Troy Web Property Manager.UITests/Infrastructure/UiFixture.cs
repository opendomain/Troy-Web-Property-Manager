using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Troy_Web_Property_Manager.Areas.Identity;
using Troy_Web_Property_Manager.Data;
using Troy_Web_Property_Manager.Models;

// Each collection starts its own app, browsers and database; one at a time keeps the machine (and LocalDB) calm.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Troy_Web_Property_Manager.UITests.Infrastructure
{
    /// <summary>A login the tests can use.</summary>
    public sealed record TestUser(string Email, string Password, string Role)
    {
        public bool IsManager => Role == AppRoles.PropertyManager;
    }

    /// <summary>
    /// One running copy of the app (and its database) shared by every UI test, started once per test run.
    /// Tests get a fresh browser each (<see cref="NewBrowser"/>), and create their own users, properties and units
    /// with unique names, so they don't depend on each other or on the order they run in.
    /// </summary>
    public class UiFixture : IAsyncLifetime
    {
        public UiFixture() : this(new AppFactory())
        {
        }

        /// <summary>For a fixture that runs the app with different settings (xUnit allows one public constructor).</summary>
        protected UiFixture(AppFactory factory)
        {
            Factory = factory;
        }

        /// <summary>Password for every user the tests create.</summary>
        public const string Password = "UiTest#2026";

        /// <summary>Demo accounts seeded by Program in Development (DemoDataSeeder).</summary>
        public static readonly TestUser SeededManager = new("manager1@example.com", DemoDataSeeder.Password, AppRoles.PropertyManager);
        public static readonly TestUser SeededApplicant = new("applicant1@example.com", DemoDataSeeder.Password, AppRoles.Applicant);

        public AppFactory Factory { get; }

        /// <summary>Where the app is listening, e.g. http://127.0.0.1:54321 (no trailing slash).</summary>
        public string BaseUrl { get; private set; } = "";

        public CapturingEmailSender Emails => Factory.Emails;

        public async Task InitializeAsync()
        {
            // Production startup requires a deployed schema; provision only this fixture's throwaway database.
            if (Factory.EnvironmentName != "Development")
            {
                var services = new ServiceCollection();
                services.AddLogging();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(Factory.ConnectionString));
                // Identity's options shape its EF model, so register it exactly the way Program does.
                services.AddAppIdentity();
                await using var provider = services.BuildServiceProvider();
                await using var scope = provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Database.MigrateAsync();
            }
            // Kestrel on a free port instead of the in-memory TestServer, so a real browser can connect.
            Factory.UseKestrel(0);
            Factory.StartServer();
            BaseUrl = Factory.ClientOptions.BaseAddress.ToString().TrimEnd('/');

            // Program's startup has already created, migrated and seeded the database by now. Make sure it was the
            // throwaway test database and not the development one from appsettings/user secrets.
            await WithDbAsync(db =>
            {
                var actual = db.Database.GetDbConnection().Database;
                if (actual != Factory.DatabaseName)
                {
                    throw new InvalidOperationException(
                        $"The app is connected to '{actual}', not the test database '{Factory.DatabaseName}'. Refusing to run UI tests.");
                }
                return Task.CompletedTask;
            });
        }

        public async Task DisposeAsync()
        {
            try
            {
                await WithDbAsync(db => db.Database.EnsureDeletedAsync());
            }
            finally
            {
                await Factory.DisposeAsync();
            }
        }

        /// <summary>A new headless browser with its own cookies (so its own login).</summary>
        public Browser NewBrowser()
        {
            return new Browser(BaseUrl);
        }

        /// <summary>A name nobody else has used, for properties, units and so on. Kept short (columns are 50 chars).</summary>
        public static string Unique(string prefix)
        {
            return $"{prefix} {Guid.NewGuid().ToString("N")[..8]}";
        }

        /// <summary>
        /// Creates a confirmed account in <paramref name="role"/>. Registration itself is covered by the account tests
        /// through the real sign-up page; everywhere else this is the quick way to get a fresh user.
        /// </summary>
        public async Task<TestUser> CreateUserAsync(string role, string label = "user")
        {
            var email = $"{label}-{Guid.NewGuid().ToString("N")[..10]}@uitest.local".ToLowerInvariant();
            using var scope = Factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            Check(await users.CreateAsync(user, Password));
            Check(await users.AddToRoleAsync(user, role));
            return new TestUser(email, Password, role);
        }

        public Task<TestUser> CreateManagerAsync(string label = "manager")
        {
            return CreateUserAsync(AppRoles.PropertyManager, label);
        }

        public Task<TestUser> CreateApplicantAsync(string label = "applicant")
        {
            return CreateUserAsync(AppRoles.Applicant, label);
        }

        /// <summary>Direct database access for arranging a test or checking something the UI doesn't show.</summary>
        public async Task WithDbAsync(Func<ApplicationDbContext, Task> action)
        {
            using var scope = Factory.Services.CreateScope();
            await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        public async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> query)
        {
            using var scope = Factory.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        /// <summary>
        /// Loads <paramref name="path"/> and requests every local stylesheet and script it references, including the
        /// local fallbacks for scripts loaded from a CDN (written in with document.write if the CDN copy didn't load).
        /// Returns the ones that don't answer 200 (with their status), so a test can assert the list is empty.
        /// </summary>
        public async Task<List<string>> BrokenAssetsAsync(string path)
        {
            using var http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
            var html = await http.GetStringAsync(path);
            var tags = Regex.Matches(html, "<(?:link[^>]*\\shref|script[^>]*\\ssrc)=\"(/[^/\"][^\"]*)\"");
            var fallbacks = Regex.Matches(html, "src=\\\\u0022(/[^/\\\\][^\\\\]*)\\\\u0022");
            var assets = tags.Concat(fallbacks)
                .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
                .Distinct()
                .ToList();
            if (assets.Count == 0) throw new InvalidOperationException($"{path} references no local stylesheets or scripts.");

            var broken = new List<string>();
            foreach (var asset in assets)
            {
                using var response = await http.GetAsync(asset);
                if (response.StatusCode != HttpStatusCode.OK) broken.Add($"{asset} -> {(int)response.StatusCode}");
            }
            return broken;
        }

        private static void Check(IdentityResult result)
        {
            if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }

    /// <summary>
    /// A second copy of the app with no SendGrid key configured (a fresh clone, or a server someone forgot to set up).
    /// Register skips the email sender entirely then, so this is the only way to reach that branch. It needs its own
    /// app because the key is read once, at startup.
    /// </summary>
    public sealed class NoSendGridKeyUiFixture() : UiFixture(new AppFactory(sendGridApiKey: ""))
    {
    }

    /// <summary>
    /// The app with Registration:ShowConfirmationLink on, so the confirmation page shows the link even after the email
    /// was sent. It needs its own app for the same reason as <see cref="NoSendGridKeyUiFixture"/>.
    /// </summary>
    public sealed class ShowConfirmationLinkUiFixture() : UiFixture(new AppFactory(showConfirmationLink: true))
    {
    }

    /// <summary>Every UI test class is in this collection, so they share one app and run one at a time.</summary>
    /// <summary>
    /// The app with the GoatCounter page counter switched on. The URL is a reserved .invalid name, and the tests only
    /// read the HTML (no browser runs the script), so nothing is ever counted.
    /// </summary>
    /// <remarks>The startup ping is on too, sent to a <see cref="StartupPingCatcher"/> on this machine.</remarks>
    public sealed class TelemetryUiFixture : UiFixture
    {
        public const string GoatCounterUrl = "https://uitest.invalid/count";

        public TelemetryUiFixture() : this(new StartupPingCatcher())
        {
        }

        private TelemetryUiFixture(StartupPingCatcher startupPing)
            : base(new AppFactory(goatCounterUrl: GoatCounterUrl, environment: "Production", startupPingUrl: startupPing.Url))
        {
            StartupPing = startupPing;
        }

        public StartupPingCatcher StartupPing { get; }
    }

    public sealed class ProductionUiFixture() : UiFixture(new AppFactory(showConfirmationLink: true, environment: "Production")) { }
    public sealed class ProductionNoEmailUiFixture() : UiFixture(new AppFactory(sendGridApiKey: "", showConfirmationLink: true, environment: "Production")) { }
    /// <summary>Telemetry on in Development, with a startup ping URL that nothing answers, so the ping fails.</summary>
    public sealed class DevelopmentTelemetryUiFixture() : UiFixture(new AppFactory(goatCounterUrl: TelemetryUiFixture.GoatCounterUrl,
        startupPingUrl: StartupPingCatcher.UnreachableUrl())) { }

    /// <summary>
    /// Any environment that isn't Development should get Production's safeguards. Telemetry is on in config here, to
    /// check it still stays off: it's only for Development and Production.
    /// </summary>
    public sealed class StagingUiFixture() : UiFixture(new AppFactory(showConfirmationLink: true,
        goatCounterUrl: TelemetryUiFixture.GoatCounterUrl, environment: "Staging")) { }

    [CollectionDefinition(Name)]
    public sealed class StagingUiCollection : ICollectionFixture<StagingUiFixture>
    {
        public const string Name = "UI (Staging)";
    }
    [CollectionDefinition(Name)]
    public sealed class ProductionUiCollection : ICollectionFixture<ProductionUiFixture>
    {
        public const string Name = "UI (Production)";
    }
    [CollectionDefinition(Name)]
    public sealed class ProductionNoEmailUiCollection : ICollectionFixture<ProductionNoEmailUiFixture>
    {
        public const string Name = "UI (Production without email)";
    }
    [CollectionDefinition(Name)]
    public sealed class DevelopmentTelemetryUiCollection : ICollectionFixture<DevelopmentTelemetryUiFixture>
    {
        public const string Name = "UI (Development telemetry on)";
    }

    [CollectionDefinition(Name)]
    public sealed class UiCollection : ICollectionFixture<UiFixture>
    {
        public const string Name = "UI";
    }

    /// <summary>Tests that need the app running without a SendGrid key (<see cref="NoSendGridKeyUiFixture"/>).</summary>
    [CollectionDefinition(Name)]
    public sealed class NoSendGridKeyUiCollection : ICollectionFixture<NoSendGridKeyUiFixture>
    {
        public const string Name = "UI (no SendGrid key)";
    }

    /// <summary>Tests that need Registration:ShowConfirmationLink on (<see cref="ShowConfirmationLinkUiFixture"/>).</summary>
    [CollectionDefinition(Name)]
    public sealed class ShowConfirmationLinkUiCollection : ICollectionFixture<ShowConfirmationLinkUiFixture>
    {
        public const string Name = "UI (show confirmation link)";
    }

    /// <summary>Tests that need the GoatCounter page counter on (<see cref="TelemetryUiFixture"/>).</summary>
    [CollectionDefinition(Name)]
    public sealed class TelemetryUiCollection : ICollectionFixture<TelemetryUiFixture>
    {
        public const string Name = "UI (telemetry on)";
    }
}
