using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

public partial class Lease
{
    public int Id { get; set; }

    public int UnitId { get; set; }

    public int RentalApplicationId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public virtual RentalApplication RentalApplication { get; set; } = null!;

    public virtual Unit Unit { get; set; } = null!;
}
