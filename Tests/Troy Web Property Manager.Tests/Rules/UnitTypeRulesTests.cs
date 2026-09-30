using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Rules;

namespace Troy_Web_Property_Manager.Tests.Rules
{
    public class UnitTypeRulesTests
    {
        private static readonly UnitType Active = new() { Id = 1, Name = "Apartment", IsActive = true };
        private static readonly UnitType Inactive = new() { Id = 2, Name = "Loft", IsActive = false };

        [Theory]
        [InlineData(null)] // new unit
        [InlineData(2)]    // unit currently has another type
        public void ActiveType_CanAlwaysBeAssigned(int? currentUnitTypeId)
        {
            Assert.True(UnitTypeRules.CanAssign(Active, currentUnitTypeId));
        }

        [Fact]
        public void InactiveType_CannotBeChosenForNewUnit()
        {
            Assert.False(UnitTypeRules.CanAssign(Inactive, currentUnitTypeId: null));
        }

        [Fact]
        public void InactiveType_CannotBeChosenForUnitWithAnotherType()
        {
            Assert.False(UnitTypeRules.CanAssign(Inactive, currentUnitTypeId: Active.Id));
        }

        [Fact]
        public void InactiveType_CanStayOnUnitThatAlreadyHasIt()
        {
            Assert.True(UnitTypeRules.CanAssign(Inactive, currentUnitTypeId: Inactive.Id));
        }
    }
}
