namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>One unit row on a property card on the Properties page.</summary>
    public class UnitViewModel
    {
        public int Id { get; set; }
        public string UnitNumber { get; set; } = "";
        public int Bedrooms { get; set; }
        public decimal MonthlyRent { get; set; }
        public string UnitTypeName { get; set; } = "";
        /// <summary>So the view can tag an inactive type with "(inactive)" - it still shows on units that have it (2.c).</summary>
        public bool UnitTypeIsActive { get; set; }

        /// <summary>Worked out in SQL from the lease dates, never stored, so it can't go stale when a lease ends (2.d).</summary>
        public bool IsLeased { get; set; }
    }
}
