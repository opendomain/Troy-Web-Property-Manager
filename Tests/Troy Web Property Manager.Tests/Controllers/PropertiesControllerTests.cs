using Microsoft.AspNetCore.Mvc;
using Troy_Web_Property_Manager.Controllers;
using Troy_Web_Property_Manager.Services;
using Troy_Web_Property_Manager.ViewModels;
using static Troy_Web_Property_Manager.Tests.TestDatabase;

namespace Troy_Web_Property_Manager.Tests.Controllers
{
    /// <summary>Every property and unit action answers 404 for a property or unit that doesn't exist (or was just removed).</summary>
    public sealed class PropertiesControllerTests : IDisposable
    {
        private const int Missing = 9999;

        private readonly TestDatabase _db = new();

        public void Dispose()
        {
            _db.Dispose();
        }

        private PropertiesController Controller()
        {
            return new PropertiesController(new PropertyService(_db.CreateContext())).SignedInAs(ManagerUser);
        }

        [Fact]
        public async Task PropertyActions_UnknownProperty_AreNotFound()
        {
            Assert.IsType<NotFoundResult>(await Controller().Card(Missing));
            Assert.IsType<NotFoundResult>(await Controller().Edit(Missing));
            Assert.IsType<NotFoundResult>(await Controller().Edit(new PropertyFormViewModel { Id = Missing, Name = "X", Address = "Y" }));
            Assert.IsType<NotFoundResult>(await Controller().DeleteConfirmed(Missing));
        }

        [Fact]
        public async Task UnitActions_UnknownUnit_AreNotFound()
        {
            Assert.IsType<NotFoundResult>(await Controller().EditUnit(Missing, _db.PropertyId));
            var unit = new UnitFormViewModel
            {
                Id = Missing,
                PropertyId = _db.PropertyId,
                UnitNumber = "301",
                Bedrooms = 1,
                MonthlyRent = 900m,
                UnitTypeId = _db.ActiveUnitTypeId
            };
            Assert.IsType<NotFoundResult>(await Controller().EditUnit(unit));
            Assert.IsType<NotFoundResult>(await Controller().DeleteUnitConfirmed(Missing));
        }
    }
}
