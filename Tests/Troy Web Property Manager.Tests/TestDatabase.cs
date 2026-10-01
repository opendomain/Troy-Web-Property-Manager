using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Troy_Web_Property_Manager.Data;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;

namespace Troy_Web_Property_Manager.Tests
{
    /// <summary>
    /// A throwaway in-memory SQLite database built from the real ApplicationDbContext model, so foreign keys, unique
    /// indexes and the Status concurrency token all actually work. It sticks around as long as the connection is open.
    /// It always has the status lookup. With sample data (the default) you also get two unit types, five users (with roles), one
    /// property and two units; without it you only get the lookups Program seeds, like a brand new database.
    /// </summary>
    public sealed class TestDatabase : IDisposable
    {
        public static readonly CurrentUser ApplicantUser = new("applicant-1", IsManager: false);
        public static readonly CurrentUser OtherApplicantUser = new("applicant-2", IsManager: false);
        public static readonly CurrentUser ThirdApplicantUser = new("applicant-3", IsManager: false);
        public static readonly CurrentUser ManagerUser = new("manager-1", IsManager: true);
        public static readonly CurrentUser OtherManagerUser = new("manager-2", IsManager: true);

        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private readonly List<ApplicationDbContext> _contexts = [];

        public int PropertyId { get; }
        public int UnitId { get; }
        public int SecondUnitId { get; }
        public int ActiveUnitTypeId { get; }
        public int InactiveUnitTypeId { get; }

        public TestDatabase(bool withSampleData = true)
        {
            _connection.Open();
            var db = CreateContext();
            db.Database.EnsureCreated();

            db.Statuses.AddRange(Enum.GetValues<ApplicationStatus>().Select(s => new Status { Id = (long)s, Name = s.ToString() }));
            if (!withSampleData)
            {
                // The unit types Program.SeedLookups adds.
                db.UnitTypes.AddRange(
                    new UnitType { Name = "Apartment", IsActive = true },
                    new UnitType { Name = "Studio", IsActive = true },
                    new UnitType { Name = "Townhouse", IsActive = true },
                    new UnitType { Name = "Loft", IsActive = false });
                db.SaveChanges();
                return;
            }

            // Real roles too, since adding an applicant to an application checks the account is an applicant.
            var roles = AppRoles.All.ToDictionary(r => r, r => new IdentityRole(r) { NormalizedName = r.ToUpperInvariant() });
            db.Roles.AddRange(roles.Values);
            foreach (var user in new[] { ApplicantUser, OtherApplicantUser, ThirdApplicantUser, ManagerUser, OtherManagerUser })
            {
                var email = $"{user.Id}@example.com";
                // Normalized like Identity does it, so lookups by email work the same as in the app.
                db.Users.Add(new IdentityUser { Id = user.Id, UserName = email, Email = email, NormalizedEmail = email.ToUpperInvariant() });
                db.UserRoles.Add(new IdentityUserRole<string>
                {
                    UserId = user.Id,
                    RoleId = roles[user.IsManager ? AppRoles.PropertyManager : AppRoles.Applicant].Id
                });
            }

            var active = new UnitType { Name = "Apartment", IsActive = true };
            var inactive = new UnitType { Name = "Loft", IsActive = false };
            var property = new Property { Name = "Riverside", Address = "1 River St" };
            var unit = new Unit { UnitNumber = "101", Bedrooms = 2, MonthlyRent = 1500m, UnitType = active, Property = property };
            var secondUnit = new Unit { UnitNumber = "102", Bedrooms = 1, MonthlyRent = 1200m, UnitType = active, Property = property };
            db.AddRange(inactive, unit, secondUnit);
            db.SaveChanges();

            PropertyId = property.Id;
            UnitId = unit.Id;
            SecondUnitId = secondUnit.Id;
            ActiveUnitTypeId = active.Id;
            InactiveUnitTypeId = inactive.Id;
        }

        /// <summary>A fresh context, like you'd get on a new request. Gets disposed along with the database.</summary>
        public ApplicationDbContext CreateContext(params IInterceptor[] interceptors)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(interceptors)
                .Options;
            var context = new ApplicationDbContext(options);
            _contexts.Add(context);
            return context;
        }

        public void Dispose()
        {
            foreach (var context in _contexts) context.Dispose();
            _connection.Dispose();
        }
    }
}
