namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>
    /// One row on the applicant's Available units page (2.b). Only units with no lease covering today show up (2.d) -
    /// <c>PropertyService.GetAvailableUnitsAsync</c> does that filtering in SQL.
    /// </summary>
    public class AvailableUnitViewModel
    {
        public int Id { get; set; }
        public string PropertyName { get; set; } = "";
        public string UnitNumber { get; set; } = "";
        public int Bedrooms { get; set; }
        public decimal MonthlyRent { get; set; }
        public string UnitTypeName { get; set; } = "";
    }
}
