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

        private async Task<(bool Promoted, IList<string> Roles)> PromoteAsync(string? email, bool failUpdates = false)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddScoped(_ => _db.CreateContext());
            var identity = services.AddIdentityCore<IdentityUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            if (failUpdates) identity.AddUserValidator<RejectEveryUpdate>();
            await using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            var promoted = await ManagerBootstrapper.PromoteAsync(users, email, _logger);
            var user = await users.FindByEmailAsync(ApplicantEmail);
            return (promoted, await users.GetRolesAsync(user!));
        }

        /// <summary>Makes Identity refuse every change to a user, like a store or validator error would.</summary>
        private sealed class RejectEveryUpdate : IUserValidator<IdentityUser>
        {
            public Task<IdentityResult> ValidateAsync(UserManager<IdentityUser> manager, IdentityUser user)
            {
                return Task.FromResult(IdentityResult.Failed(new IdentityError { Description = "Refused by the test." }));
            }
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

        [Fact]
        public async Task ConfirmedAccountWithNoRole_BecomesManager()
        {
            ConfirmApplicantEmail();
            using (var db = _db.CreateContext())
            {
                db.UserRoles.RemoveRange(db.UserRoles.Where(r => r.UserId == TestDatabase.ApplicantUser.Id));
                db.SaveChanges();
            }

            var (promoted, roles) = await PromoteAsync(ApplicantEmail);

            Assert.True(promoted);
            Assert.Equal([AppRoles.PropertyManager], roles);
        }

        [Fact]
        public async Task IdentityRefusingTheChange_StopsStartupWithTheReason()
        {
            ConfirmApplicantEmail();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => PromoteAsync(ApplicantEmail, failUpdates: true));

            Assert.Contains($"Couldn't make {ApplicantEmail} a Property Manager", ex.Message);
            Assert.Contains("Refused by the test.", ex.Message);
        }
    }
}
