using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Troy_Web_Property_Manager.Data;
using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.Tests.Data
{
    /// <summary>Bootstrap:ManagerEmail is the only way to get a Property Manager outside Development.</summary>
    public sealed class ManagerBootstrapperTests : IDisposable
    {
        private static readonly string ApplicantEmail = $"{TestDatabase.ApplicantUser.Id}@example.com";

        private readonly TestDatabase _db = new();
        private readonly TestLogger<ManagerBootstrapperTests> _logger = new();

        public void Dispose()
        {
            _db.Dispose();
        }

        private async Task<(bool Promoted, IList<string> Roles)> PromoteAsync(string? email)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddScoped(_ => _db.CreateContext());
            services.AddIdentityCore<IdentityUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            await using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            var promoted = await ManagerBootstrapper.PromoteAsync(users, email, _logger);
            var user = await users.FindByEmailAsync(ApplicantEmail);
            return (promoted, await users.GetRolesAsync(user!));
        }

        private void ConfirmApplicantEmail()
        {
            using var db = _db.CreateContext();
            db.Users.Single(u => u.Id == TestDatabase.ApplicantUser.Id).EmailConfirmed = true;
            db.SaveChanges();
        }

        [Fact]
        public async Task ConfirmedApplicant_BecomesManagerOnly()
        {
            ConfirmApplicantEmail();

            var (promoted, roles) = await PromoteAsync(ApplicantEmail);

            Assert.True(promoted);
            Assert.Equal([AppRoles.PropertyManager], roles);
        }

        [Fact]
        public async Task AlreadyManager_IsLeftAlone()
        {
            ConfirmApplicantEmail();
            await PromoteAsync(ApplicantEmail);

            var (promoted, roles) = await PromoteAsync(ApplicantEmail);

            Assert.False(promoted);
            Assert.Equal([AppRoles.PropertyManager], roles);
        }

        [Fact]
        public async Task UnconfirmedAccount_IsNotPromoted()
        {
            var (promoted, roles) = await PromoteAsync(ApplicantEmail);

            Assert.False(promoted);
            Assert.Equal([AppRoles.Applicant], roles);
            Assert.Contains(_logger.Messages, m => m.Message.Contains("isn't a confirmed account"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("nobody@example.com")]
        public async Task NoSettingOrUnknownEmail_ChangesNothing(string? email)
        {
            ConfirmApplicantEmail();

            var (promoted, roles) = await PromoteAsync(email);

            Assert.False(promoted);
            Assert.Equal([AppRoles.Applicant], roles);
        }
    }
}
