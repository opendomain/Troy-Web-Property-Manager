using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;
using Troy_Web_Property_Manager.ViewModels;
using static Troy_Web_Property_Manager.Tests.TestDatabase;

namespace Troy_Web_Property_Manager.Tests.Services
{
    public sealed class PropertyServiceTests : IDisposable
    {
        private readonly TestDatabase _db = new();
        private readonly TestLogger<PropertyService> _log = new();

        public void Dispose()
        {
            _db.Dispose();
        }

        private PropertyService Service()
        {
            return new(_db.CreateContext(), logger: _log);
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
        public async Task GetProperty_ReturnsOneCardWithItsUnits()
        {
            var property = await Service().GetPropertyAsync(_db.PropertyId);

            Assert.NotNull(property);
            Assert.Equal(new[] { "101", "102" }, property.Units.Select(u => u.UnitNumber));
            Assert.Null(await Service().GetPropertyAsync(9999));
        }

        [Fact]
        public async Task AvailableUnits_FiltersByProperty()
        {
            Assert.Equal(2, (await Service().GetAvailableUnitsAsync(_db.PropertyId)).Count);
            Assert.Empty(await Service().GetAvailableUnitsAsync(9999));
        }

        [Fact]
        public async Task AvailableUnits_FiltersByMinimumBedrooms()
        {
            // Unit 101 has 2 bedrooms, unit 102 has 1.
            Assert.Equal(2, (await Service().GetAvailableUnitsAsync(minBedrooms: 1)).Count);
            Assert.Equal(new[] { _db.UnitId }, (await Service().GetAvailableUnitsAsync(minBedrooms: 2)).Select(u => u.Id));
            Assert.Empty(await Service().GetAvailableUnitsAsync(minBedrooms: 3));
        }

        [Fact]
        public async Task AvailableUnits_CombinesPropertyAndBedroomFilters()
        {
            Assert.Single(await Service().GetAvailableUnitsAsync(_db.PropertyId, minBedrooms: 2));
            Assert.Empty(await Service().GetAvailableUnitsAsync(9999, minBedrooms: 1));
        }

        /// <summary>
        /// Adds a second property so every sort has something to do, including ties. The four units, by label:
        /// "R101" Riverside 101 (2 bed, $1500, Apartment), "R102" Riverside 102 (1 bed, $1200, Apartment),
        /// "BA1" Birch Court A1 (3 bed, $2100, Loft), "BA2" Birch Court A2 (1 bed, $1200, Apartment).
        /// </summary>
        private async Task<Dictionary<string, int>> SortableUnitsAsync()
        {
            var db = _db.CreateContext();
            var birch = new Property { Name = "Birch Court", Address = "2 Birch Rd" };
            var a1 = new Unit { UnitNumber = "A1", Bedrooms = 3, MonthlyRent = 2100m, UnitTypeId = _db.InactiveUnitTypeId, Property = birch };
            var a2 = new Unit { UnitNumber = "A2", Bedrooms = 1, MonthlyRent = 1200m, UnitTypeId = _db.ActiveUnitTypeId, Property = birch };
            db.AddRange(a1, a2);
            await db.SaveChangesAsync();
            return new() { ["R101"] = _db.UnitId, ["R102"] = _db.SecondUnitId, ["BA1"] = a1.Id, ["BA2"] = a2.Id };
        }

        [Theory]
        // Default: property, then unit number.
        [InlineData(UnitSortColumn.Property, SortDirection.Asc, "BA1 BA2 R101 R102")]
        [InlineData(UnitSortColumn.Property, SortDirection.Desc, "R102 R101 BA2 BA1")]
        // Ties (two 1-bedroom units, two at $1200, three Apartments) stay in property/unit order in both directions.
        [InlineData(UnitSortColumn.Bedrooms, SortDirection.Asc, "BA2 R102 R101 BA1")]
        [InlineData(UnitSortColumn.Bedrooms, SortDirection.Desc, "BA1 R101 BA2 R102")]
        [InlineData(UnitSortColumn.Rent, SortDirection.Asc, "BA2 R102 R101 BA1")]
        [InlineData(UnitSortColumn.Rent, SortDirection.Desc, "BA1 R101 BA2 R102")]
        [InlineData(UnitSortColumn.Type, SortDirection.Asc, "BA2 R101 R102 BA1")]
        [InlineData(UnitSortColumn.Type, SortDirection.Desc, "BA1 BA2 R101 R102")]
        public async Task AvailableUnits_SortsByEachColumn(UnitSortColumn sort, SortDirection dir, string expected)
        {
            var units = await SortableUnitsAsync();

            var result = await Service().GetAvailableUnitsAsync(sort: sort, dir: dir);

            Assert.Equal(expected.Split(' ').Select(label => units[label]), result.Select(u => u.Id));
        }

        [Fact]
        public async Task AvailableUnits_SortsWithinTheFilters()
        {
            var units = await SortableUnitsAsync();

            var result = await Service().GetAvailableUnitsAsync(_db.PropertyId, minBedrooms: 1, UnitSortColumn.Rent, SortDirection.Desc);

            Assert.Equal(new[] { units["R101"], units["R102"] }, result.Select(u => u.Id));
        }

        // ---------------- Logging ----------------

        [Fact]
        public async Task Logs_AddingAndRemovingAUnit()
        {
            var saved = await Service().SaveUnitAsync(NewUnit("301"));
            AssertOk(saved);
            AssertOk(await Service().DeleteUnitAsync(saved.Id));

            Assert.True(_log.Has(LogLevel.Information, $"Unit {saved.Id} added to property {_db.PropertyId}."));
            Assert.True(_log.Has(LogLevel.Information, $"Unit {saved.Id} removed from property {_db.PropertyId}."));
        }

        [Fact]
        public async Task Logs_AWarning_WhenAnInactiveTypeIsPosted()
        {
            Assert.False((await Service().SaveUnitAsync(NewUnit("301", _db.InactiveUnitTypeId))).Succeeded);

            Assert.True(_log.Has(LogLevel.Warning, $"unit type {_db.InactiveUnitTypeId} is inactive or doesn't exist."));
        }
    }
}
