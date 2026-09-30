namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>
    /// A property card (with its units) on the manager's Properties page. Display only - the add/edit forms use
    /// <see cref="PropertyFormViewModel"/> and <see cref="UnitFormViewModel"/>.
    /// </summary>
    public class PropertyViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public List<UnitViewModel> Units { get; set; } = [];
    }
}
