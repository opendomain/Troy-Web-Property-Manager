using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.Rules
{
    /// <summary>
    /// The Unit Type rule (2.c): an inactive type still shows on a unit that already has it, but can't be picked for
    /// any other unit. It's a plain function so we can unit test it, and so <c>PropertyService.SaveUnitAsync</c> can
    /// enforce it on the server no matter what the dropdown showed.
    /// </summary>
    public static class UnitTypeRules
    {
        /// <summary>An inactive type is only OK if the unit already has it. (currentUnitTypeId is null for a new unit.)</summary>
        public static bool CanAssign(UnitType type, int? currentUnitTypeId)
        {
            return type.IsActive || type.Id == currentUnitTypeId;
        }
    }
}
