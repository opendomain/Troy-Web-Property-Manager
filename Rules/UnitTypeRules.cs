using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.Rules
{
    public static class UnitTypeRules
    {
        /// <summary>An inactive type is allowed only if it is already the unit's current type (currentUnitTypeId is null for a new unit).</summary>
        public static bool CanAssign(UnitType type, int? currentUnitTypeId)
        {
            return type.IsActive || type.Id == currentUnitTypeId;
        }
    }
}
