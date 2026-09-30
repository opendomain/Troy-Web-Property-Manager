using Microsoft.EntityFrameworkCore;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;
using static Troy_Web_Property_Manager.Tests.TestDatabase;

namespace Troy_Web_Property_Manager.Tests.Data
{
    /// <summary>The database's own check constraints, hit directly so no service or view model gets in the way.</summary>
    public sealed class CheckConstraintTests : IDisposable
    {
        private readonly TestDatabase _db = new();

        public void Dispose()
        {
            _db.Dispose();
        }

        private async Task<int> StartApplicationAsync()
        {
            return (await new ApplicationService(_db.CreateContext()).StartAsync(_db.UnitId, ApplicantUser)).Id;
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(11)]
        public async Task Bedrooms_outside_0_to_10_are_rejected(int bedrooms)
        {
            var db = _db.CreateContext();
            var unit = await db.Units.SingleAsync(u => u.Id == _db.UnitId);
            unit.Bedrooms = bedrooms;

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task A_residence_cannot_end_before_it_starts()
        {
            var applicationId = await StartApplicationAsync();
            var db = _db.CreateContext();
            db.Residences.Add(new Residence
            {
                RentalApplicationId = applicationId,
                Address = "5 Hill St",
                LandlordName = "Pat",
                LandlordPhone = "518-555-0100",
                MoveInDate = new DateOnly(2024, 5, 1),
                MoveOutDate = new DateOnly(2024, 4, 30)
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task A_lease_has_to_end_after_it_starts()
        {
            var applicationId = await StartApplicationAsync();
            var db = _db.CreateContext();
            var start = new DateTime(2026, 9, 30);
            db.Leases.Add(new Lease { RentalApplicationId = applicationId, UnitId = _db.UnitId, StartDate = start, EndDate = start });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }
}
