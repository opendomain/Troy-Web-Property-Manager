using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

public partial class ApplicationStatusHistory
{
    public int Id { get; set; }

    public long PreviousStatus { get; set; }

    public long NewStatus { get; set; }

    /// <summary>Optional for status changes; required by the review rules for Return and Deny.</summary>
    public string? Comment { get; set; }

    /// <summary>The review outcome, when this change came from a property manager's review.</summary>
    public ReviewOutcome? Outcome { get; set; }

    public DateTime ChangedDate { get; set; }

    /// <summary>Identity user id of whoever made the change.</summary>
    public string ChangedByUser { get; set; } = null!;

    public int RentalApplicationId { get; set; }

    public virtual RentalApplication RentalApplication { get; set; } = null!;
}
