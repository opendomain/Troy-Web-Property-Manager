using Microsoft.EntityFrameworkCore;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;
using Troy_Web_Property_Manager.ViewModels;
using static Troy_Web_Property_Manager.Tests.TestDatabase;

namespace Troy_Web_Property_Manager.Tests.Services
{
    public sealed class PropertyServiceTests : IDisposable
    {
        private readonly TestDatabase _db = new();

        public void Dispose()
        {
            _db.Dispose();
        }

        private PropertyService Service()
        {
            return new(_db.CreateContext());
        }

        private static void AssertOk(ServiceResult result)
        {
            Assert.True(result.Succeeded, $"NotFound={result.NotFound}; {string.Join("; ", result.Errors.Values)}");
        }

        private UnitFormViewModel NewUnit(string number = "201", int? unitTypeId = null)
        {
            return new()
            {
                PropertyId = _db.PropertyId,
                UnitNumber = number,
                Bedrooms = 0,
                MonthlyRent = 950m,
                UnitTypeId = unitTypeId ?? _db.ActiveUnitTypeId
            };
        }

        /// <summary>Gives the unit an application, which blocks removing it or its property.</summary>
        private Task StartApplicationAsync(int unitId)
        {
            return new ApplicationService(_db.CreateContext()).StartAsync(unitId, ApplicantUser);
        }

        // ---------------- Properties ----------------

        [Fact]
        public async Task SaveProperty_AddsAndEditsWithTrimmedValues()
        {
            var added = await Service().SavePropertyAsync(new PropertyFormViewModel { Name = " Hilltop ", Address = " 2 Hill Rd " });
            AssertOk(added);
            AssertOk(await Service().SavePropertyAsync(new PropertyFormViewModel { Id = added.Id, Name = "Hilltop Commons", Address = "2 Hill Rd" }));

            var form = await Service().GetPropertyFormAsync(added.Id);
            Assert.Equal("Hilltop Commons", form!.Name);
            Assert.Equal("2 Hill Rd", form.Address);
        }

        [Fact]
        public async Task SaveProperty_UnknownId_IsNotFound()
        {
            Assert.True((await Service().SavePropertyAsync(new PropertyFormViewModel { Id = 9999, Name = "X", Address = "Y" })).NotFound);
        }

        [Fact]
        public async Task DeleteProperty_WithoutApplications_RemovesItAndItsUnits()
        {
            AssertOk(await Service().DeletePropertyAsync(_db.PropertyId));

            var db = _db.CreateContext();
            Assert.Empty(await db.Properties.ToListAsync());
            Assert.Empty(await db.Units.ToListAsync());
        }

        [Fact]
        public async Task DeleteProperty_WithApplications_IsRejected()
        {
            await StartApplicationAsync(_db.UnitId);

            var result = await Service().DeletePropertyAsync(_db.PropertyId);

            Assert.False(result.Succeeded);
            Assert.True(await _db.CreateContext().Properties.AnyAsync());
        }

        // ---------------- Units ----------------

        [Fact]
        public async Task SaveUnit_AddsUnitWithZeroBedrooms()
        {
            var result = await Service().SaveUnitAsync(NewUnit());
            AssertOk(result);

            var unit = await _db.CreateContext().Units.SingleAsync(u => u.Id == result.Id);
            Assert.Equal(0, unit.Bedrooms); // not replaced by the column default of 1
            Assert.Equal(950m, unit.MonthlyRent);
        }

        [Fact]
        public async Task SaveUnit_UnknownProperty_IsNotFound()
        {
            var model = NewUnit();
            model.PropertyId = 9999;
            Assert.True((await Service().SaveUnitAsync(model)).NotFound);
        }

        [Theory]
        [InlineData("101")]
        [InlineData(" 101 ")]
        public async Task SaveUnit_DuplicateNumberAtProperty_IsRejected(string number)
        {
            var result = await Service().SaveUnitAsync(NewUnit(number));

            Assert.False(result.Succeeded);
            Assert.True(result.Errors.ContainsKey(nameof(UnitFormViewModel.UnitNumber)));
        }

        [Fact]
        public async Task SaveUnit_KeepingItsOwnNumber_IsAllowed()
        {
            var edit = (await Service().GetUnitFormAsync(_db.UnitId))!;
            edit.MonthlyRent = 1600m;

            AssertOk(await Service().SaveUnitAsync(edit));
        }

        [Fact]
        public async Task SaveUnit_InactiveTypeForNewUnit_IsRejected()
        {
            var result = await Service().SaveUnitAsync(NewUnit(unitTypeId: _db.InactiveUnitTypeId));

            Assert.False(result.Succeeded);
            Assert.True(result.Errors.ContainsKey(nameof(UnitFormViewModel.UnitTypeId)));
        }

        [Fact]
        public async Task SaveUnit_UnitAlreadyOfInactiveType_CanKeepIt()
        {
            var db = _db.CreateContext();
            var unit = await db.Units.SingleAsync(u => u.Id == _db.UnitId);
            unit.UnitTypeId = _db.InactiveUnitTypeId; // the type was retired after the unit was created
            await db.SaveChangesAsync();

            var edit = (await Service().GetUnitFormAsync(_db.UnitId))!;
            edit.Bedrooms = 3;
            AssertOk(await Service().SaveUnitAsync(edit));
        }

        [Fact]
        public async Task SaveUnit_SwitchingToInactiveType_IsRejected()
        {
            var edit = (await Service().GetUnitFormAsync(_db.UnitId))!;
            edit.UnitTypeId = _db.InactiveUnitTypeId;

            Assert.False((await Service().SaveUnitAsync(edit)).Succeeded);
        }

        [Fact]
        public async Task UnitTypeOptions_ShowInactiveTypeOnlyForUnitThatHasIt()
        {
            var forNewUnit = await Service().GetUnitTypeOptionsAsync(null);
            var forInactiveUnit = await Service().GetUnitTypeOptionsAsync(_db.InactiveUnitTypeId);

            Assert.Equal(new[] { "Apartment" }, forNewUnit.Select(o => o.Text));
            Assert.Equal(new[] { "Apartment", "Loft (inactive)" }, forInactiveUnit.Select(o => o.Text));
        }

        [Fact]
        public async Task DeleteUnit_WithApplications_IsRejected()
        {
            await StartApplicationAsync(_db.UnitId);

            Assert.False((await Service().DeleteUnitAsync(_db.UnitId)).Succeeded);
            AssertOk(await Service().DeleteUnitAsync(_db.SecondUnitId));
        }

        [Fact]
        public async Task Database_RejectsDuplicateUnitNumberAtProperty()
        {
            var db = _db.CreateContext();
            db.Units.Add(new Unit { PropertyId = _db.PropertyId, UnitNumber = "101", UnitTypeId = _db.ActiveUnitTypeId });

            // The unique index backs up the service's check when two saves race.
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        // ---------------- Availability ----------------

        [Fact]
        public async Task AvailableUnits_ListsUnitsWithoutActiveLease()
        {
            var db = _db.CreateContext();
            var applicationId = (await new ApplicationService(db).StartAsync(_db.UnitId, ApplicantUser)).Id;
            db.Leases.AddRange(
                // Ends today (end date is exclusive), so the unit is available again.
                new Lease { UnitId = _db.UnitId, RentalApplicationId = applicationId, StartDate = DateTime.Today.AddYears(-1), EndDate = DateTime.Today },
                // Starts today: the second unit is leased.
                new Lease { UnitId = _db.SecondUnitId, RentalApplicationId = applicationId, StartDate = DateTime.Today, EndDate = DateTime.Today.AddYears(1) });
            await db.SaveChangesAsync();

            var available = await Service().GetAvailableUnitsAsync();
            var properties = await Service().GetPropertiesAsync();

            Assert.Equal(new[] { _db.UnitId }, available.Select(u => u.Id));
            Assert.Equal(new[] { false, true }, properties.Single().Units.Select(u => u.IsLeased));
        }

        [Fact]
        public async Task AvailableUnits_FiltersByProperty()
        {
            Assert.Equal(2, (await Service().GetAvailableUnitsAsync(_db.PropertyId)).Count);
            Assert.Empty(await Service().GetAvailableUnitsAsync(9999));
        }
    }
}
