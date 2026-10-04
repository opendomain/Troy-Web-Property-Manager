using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Troy_Web_Property_Manager.Areas.Identity;
using Troy_Web_Property_Manager.Data;

namespace Troy_Web_Property_Manager.Tests.Data
{
    /// <summary>
    /// Program and the UI tests' Production fixture both register Identity through AddAppIdentity, and Identity's
    /// options feed into the EF model. If the model it builds stops matching the migrations, deploying the schema
    /// (MigrateAsync) refuses to run, so check that here rather than in a slow browser run.
    /// </summary>
    public sealed class AppIdentityTests
    {
        private static ServiceProvider Services()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            // Never opened: the model and the migrations snapshot are compared in memory.
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer("Server=unused;Database=unused"));
            services.AddAppIdentity();
            return services.BuildServiceProvider();
        }

        [Fact]
        public void Model_MatchesTheMigrations()
        {
            using var provider = Services();
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.False(db.Database.HasPendingModelChanges(),
                "The model differs from Data/Migrations. Add a migration, or register Identity the same way Program does.");
        }

        [Fact]
        public void RequiresConfirmedAccounts_AndSupportsRoles()
        {
            using var provider = Services();
            using var scope = provider.CreateScope();

            Assert.True(scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value.SignIn.RequireConfirmedAccount);
            Assert.NotNull(scope.ServiceProvider.GetService<RoleManager<IdentityRole>>());
        }
    }
}
