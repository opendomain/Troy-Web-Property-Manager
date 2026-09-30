namespace Troy_Web_Property_Manager.ViewModels
{
    public class PropertyViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public List<UnitViewModel> Units { get; set; } = [];
    }
}
