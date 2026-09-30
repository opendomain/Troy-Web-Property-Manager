using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

public partial class ApplicationStatusHistory
{
    public int Id { get; set; }

    public int PreviousStatus { get; set; }

    public int NewStatus { get; set; }

    public string Comment { get; set; } = null!;

    public DateTime ChangedDate { get; set; }

    /// <summary>Identity user id of whoever made the change.</summary>
    public string ChangedByUser { get; set; } = null!;

    public int RentalApplicationId { get; set; }

    public virtual RentApplication RentalApplication { get; set; } = null!;
}
