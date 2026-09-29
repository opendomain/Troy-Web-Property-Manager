using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

public partial class Residence
{
    public int Id { get; set; }

    public string Address { get; set; } = null!;

    public string LandlordName { get; set; } = null!;

    public string LandlordPhone { get; set; } = null!;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public int RentalApplicationId { get; set; }

    public virtual RentApplication RentalApplication { get; set; } = null!;
}
