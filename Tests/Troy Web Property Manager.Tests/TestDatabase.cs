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
    /// A private in-memory SQLite database built from the real ApplicationDbContext model, so foreign keys,
    /// unique indexes and the Status concurrency token are enforced. It lives as long as the open connection.
    /// Seeded with the status and unit type lookups, three users, one property and two units.
    /// </summary>
    public sealed class TestDatabase : IDisposable
    {
        public static readonly CurrentUser ApplicantUser = new("applicant-1", IsManager: false);
        public static readonly CurrentUser OtherApplicantUser = new("applicant-2", IsManager: false);
        public static readonly CurrentUser ManagerUser = new("manager-1", IsManager: true);

        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private readonly List<ApplicationDbContext> _contexts = [];

        public int PropertyId { get; }
        public int UnitId { get; }
        public int SecondUnitId { get; }
        public int ActiveUnitTypeId { get; }
        public int InactiveUnitTypeId { get; }

        public TestDatabase()
        {
            _connection.Open();
            var db = CreateContext();
            db.Database.EnsureCreated();

            db.Statuses.AddRange(Enum.GetValues<ApplicationStatus>().Select(s => new Status { Id = (long)s, Name = s.ToString() }));
            foreach (var user in new[] { ApplicantUser, OtherApplicantUser, ManagerUser })
            {
                db.Users.Add(new IdentityUser { Id = user.Id, UserName = $"{user.Id}@example.com", Email = $"{user.Id}@example.com" });
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

        /// <summary>A new context, like a new request scope. Disposed with the database.</summary>
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
