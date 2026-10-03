using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
}
