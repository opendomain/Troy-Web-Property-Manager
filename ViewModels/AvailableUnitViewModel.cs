namespace Troy_Web_Property_Manager.ViewModels
{
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
