namespace Troy_Web_Property_Manager.Models;

/// <summary>One prior residence in the Residence History section of an application.</summary>
public partial class Residence
{
    public int Id { get; set; }

    public string Address { get; set; } = null!;

    public string LandlordName { get; set; } = null!;

    public string LandlordPhone { get; set; } = null!;

    public DateOnly MoveInDate { get; set; }

    public DateOnly MoveOutDate { get; set; }

    public int RentalApplicationId { get; set; }

    public virtual RentalApplication RentalApplication { get; set; } = null!;
}
