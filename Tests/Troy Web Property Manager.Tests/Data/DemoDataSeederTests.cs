using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Troy_Web_Property_Manager.Data;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Rules;
using Troy_Web_Property_Manager.Services;

namespace Troy_Web_Property_Manager.Tests.Data
{
    /// <summary>The demo data has to follow the same rules as anything entered through the app.</summary>
    public sealed class DemoDataSeederTests : IDisposable
    {
        private readonly TestDatabase _db = new(withSampleData: false);

        public void Dispose()
        {
            _db.Dispose();
        }

        /// <summary>Seeds the way Program does: roles first, then the demo data through a real UserManager.</summary>
        private static async Task<bool> SeedAsync(TestDatabase db)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddScoped(_ => db.CreateContext());
            services.AddIdentityCore<IdentityUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            await using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in AppRoles.All)
            {
                if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));
            }

            return await DemoDataSeeder.SeedAsync(
                scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
                scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>());
        }

        private async Task<List<RentalApplication>> SeededApplicationsAsync()
        {
            Assert.True(await SeedAsync(_db));
            return await _db.CreateContext().RentalApplications.AsNoTracking()
                .Include(a => a.Applicant)
                .Include(a => a.ApplicantInformation)
                .Include(a => a.ApplicationStatusHistories)
                .Include(a => a.Leases)
                .Include(a => a.Residences)
                .ToListAsync();
        }

        private static List<ApplicationStatusHistory> Timeline(RentalApplication application)
        {
            return application.ApplicationStatusHistories.OrderBy(h => h.ChangedDate).ThenBy(h => h.Id).ToList();
        }

        [Fact]
        public async Task Seed_FillsEmptyDatabaseOnlyOnce()
        {
            Assert.True(await SeedAsync(_db));
            Assert.False(await SeedAsync(_db));

            var db = _db.CreateContext();
            Assert.Equal(6, await db.Properties.CountAsync());
            Assert.Equal(12, await db.Applicants.CountAsync());
            Assert.True(await db.RentalApplications.CountAsync() >= 45);
            Assert.True(await db.Leases.AnyAsync());

            // Every status shows up, so every screen and filter has something to show.
            var statuses = await db.RentalApplications.Select(a => a.Status).Distinct().ToListAsync();
            Assert.All(Enum.GetValues<ApplicationStatus>(), s => Assert.Contains((long)s, statuses));
        }

        [Fact]
        public async Task Seed_IsTheSameEveryTime()
        {
            using var other = new TestDatabase(withSampleData: false);
            Assert.True(await SeedAsync(_db));
            Assert.True(await SeedAsync(other));

            static Task<List<string>> Units(TestDatabase db)
            {
                return db.CreateContext().Units
                    .OrderBy(u => u.Id).Select(u => u.Property.Name + " " + u.UnitNumber + " " + u.Bedrooms).ToListAsync();
            }
            Assert.Equal(await Units(_db), await Units(other));
        }

        [Fact]
        public async Task DemoAccounts_CanSignInWithTheirRole()
        {
            Assert.True(await SeedAsync(_db));

            var db = _db.CreateContext();
            var hasher = new PasswordHasher<IdentityUser>();
            foreach (var (email, role) in new[] { ("manager1@example.com", AppRoles.PropertyManager), ("applicant1@example.com", AppRoles.Applicant) })
            {
                var user = await db.Users.SingleAsync(u => u.Email == email);
                Assert.True(user.EmailConfirmed); // sign-in requires a confirmed account
                Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, user.PasswordHash!, DemoDataSeeder.Password));

                var roleNames = await (from ur in db.UserRoles
                                       join r in db.Roles on ur.RoleId equals r.Id
                                       where ur.UserId == user.Id
                                       select r.Name).ToListAsync();
                Assert.Equal(new[] { role }, roleNames);
            }
        }

        [Fact]
        public async Task History_FollowsTheWorkflow()
        {
            var applications = await SeededApplicationsAsync();
            var db = _db.CreateContext();
            var managerIds = await (from ur in db.UserRoles
                                    join r in db.Roles on ur.RoleId equals r.Id
                                    where r.Name == AppRoles.PropertyManager
                                    select ur.UserId).ToListAsync();

            Assert.All(applications, application =>
            {
                var timeline = Timeline(application);
                Assert.Equal(0, timeline[0].PreviousStatus);
                Assert.Equal((long)ApplicationStatus.Draft, timeline[0].NewStatus);
                Assert.Equal(application.Created, timeline[0].ChangedDate);
                Assert.Equal(application.Status, timeline[^1].NewStatus);
                Assert.All(timeline, h => Assert.True(h.ChangedDate <= DateTime.Now));

                for (var i = 1; i < timeline.Count; i++)
                {
                    Assert.Equal(timeline[i - 1].NewStatus, timeline[i].PreviousStatus);
                    Assert.True(ApplicationWorkflow.CanTransition((ApplicationStatus)timeline[i].PreviousStatus, (ApplicationStatus)timeline[i].NewStatus));
                }

                foreach (var change in timeline)
                {
                    if (change.Outcome is { } outcome)
                    {
                        Assert.Equal((long)ApplicationWorkflow.StatusFor(outcome), change.NewStatus);
                        Assert.Contains(change.ChangedByUser, managerIds);
                        if (ApplicationWorkflow.RequiresComment(outcome)) Assert.False(string.IsNullOrWhiteSpace(change.Comment));
                    }
                    else
                    {
                        Assert.Equal(application.Applicant.UserId, change.ChangedByUser);
                    }
                }
            });
        }

        [Fact]
        public async Task Leases_OneAtATimePerUnitAndOnlyForApprovedApplications()
        {
            var applications = await SeededApplicationsAsync();

            Assert.All(applications, a =>
                Assert.Equal(a.Status == (long)ApplicationStatus.Approved ? 1 : 0, a.Leases.Count));

            var leases = applications.SelectMany(a => a.Leases).ToList();
            Assert.All(leases, l => Assert.Equal(LeaseRules.EndDateFor(l.StartDate), l.EndDate));
            foreach (var unitLeases in leases.GroupBy(l => l.UnitId))
            {
                var ordered = unitLeases.OrderBy(l => l.StartDate).ToList();
                for (var i = 1; i < ordered.Count; i++)
                {
                    Assert.True(ordered[i].StartDate >= ordered[i - 1].EndDate, $"Unit {unitLeases.Key} has overlapping leases.");
                }
            }

            // Both active and expired leases, so available and leased units both show up.
            Assert.Contains(leases, l => LeaseRules.IsActiveOn(l, DateTime.Today));
            Assert.Contains(leases, l => !LeaseRules.IsActiveOn(l, DateTime.Today));
        }

        [Fact]
        public async Task NothingIsSubmittedWhileItsUnitIsLeased()
        {
            var applications = await SeededApplicationsAsync();
            var leasesByUnit = applications.SelectMany(a => a.Leases).ToLookup(l => l.UnitId);

            foreach (var application in applications)
            {
                foreach (var submit in application.ApplicationStatusHistories.Where(h => h.NewStatus == (long)ApplicationStatus.Submitted))
                {
                    Assert.DoesNotContain(leasesByUnit[application.UnitId], l => LeaseRules.IsActiveOn(l, submit.ChangedDate));
                }
            }
        }

        [Fact]
        public async Task Sections_MatchHowFarEachApplicationGot()
        {
            var applications = await SeededApplicationsAsync();

            Assert.All(applications, application =>
            {
                var submits = Timeline(application).Where(h => h.NewStatus == (long)ApplicationStatus.Submitted).ToList();
                Assert.Equal(submits.LastOrDefault()?.ChangedDate, application.Submitted);
                if (submits.Count > 0)
                {
                    Assert.True(application.ApplicantInformationSaved);
                    Assert.True(application.ResidenceHistorySaved);
                }

                Assert.Equal(application.ApplicantInformationSaved, application.ApplicantInformation is not null);
                Assert.Equal(application.ResidenceHistorySaved, application.Residences.Count > 0);
                Assert.All(application.Residences, r =>
                {
                    Assert.True(r.MoveInDate <= r.MoveOutDate);
                    Assert.True(r.MoveOutDate <= DateOnly.FromDateTime(application.Created));
                });
            });
        }

        [Fact]
        public async Task Text_FitsTheColumns()
        {
            var applications = await SeededApplicationsAsync();
            var db = _db.CreateContext();
            var properties = await db.Properties.Include(p => p.Units).ToListAsync();

            var values = properties.SelectMany(p => p.Units.Select(u => u.UnitNumber).Append(p.Name).Append(p.Address))
                .Concat(applications.Select(a => a.Applicant).Distinct()
                    .SelectMany(a => new[] { a.Name, a.Phone, a.Email, a.CurrentAddress }))
                .Concat(applications.Where(a => a.ApplicantInformation is not null)
                    .SelectMany(a => new[] { a.ApplicantInformation!.Name, a.ApplicantInformation.Phone, a.ApplicantInformation.Email, a.ApplicantInformation.CurrentAddress }))
                .Concat(applications.SelectMany(a => a.Residences).SelectMany(r => new[] { r.Address, r.LandlordName, r.LandlordPhone }));

            // SQLite doesn't care about nvarchar(50), so check it here - SQL Server would reject anything longer.
            Assert.All(values, v => Assert.InRange(v.Length, 1, 50));
        }

        [Fact]
        public async Task SeededData_WorksWithTheApplicationService()
        {
            var applications = await SeededApplicationsAsync();
            var service = new ApplicationService(_db.CreateContext());
            var manager = new CurrentUser((await _db.CreateContext().Users.SingleAsync(u => u.Email == "manager1@example.com")).Id, IsManager: true);

            // Managers only see applications that were submitted at least once; never-submitted drafts stay private.
            var submitted = applications.Where(a => a.Submitted != null).ToList();
            Assert.NotEmpty(submitted);
            Assert.Equal(submitted.Count, (await service.ListAsync(null, null, manager)).Count);
            foreach (var application in applications)
            {
                var editor = await service.GetEditorAsync(application.Id, null, manager);
                if (application.Submitted != null) Assert.NotNull(editor);
                else Assert.Null(editor);
            }
        }
    }
}
