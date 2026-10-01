namespace Troy_Web_Property_Manager.Models;

/// <summary>One prior residence in the Residence History section of an application.</summary>
/// <remarks>
/// A residence can be saved with errors and fixed later, so the dates can be empty and move-out can be before move-in.
/// Blank text is stored as "". Submit is blocked until <c>ResidenceViewModel</c>'s rules all pass.
/// </remarks>
public partial class Residence
{
    public int Id { get; set; }

    public string Address { get; set; } = null!;

    public string LandlordName { get; set; } = null!;

    public string LandlordPhone { get; set; } = null!;

    public DateOnly? MoveInDate { get; set; }

    public DateOnly? MoveOutDate { get; set; }

    public int RentalApplicationId { get; set; }

    public virtual RentalApplication RentalApplication { get; set; } = null!;
}
