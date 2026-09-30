using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

/// <summary>
/// One row for every status change and review (5.c: who, when, comment). Only
/// <c>ApplicationService.ChangeStatus</c> writes these (plus StartAsync for the first row), so nothing can change a
/// status without leaving a trail.
/// </summary>
public partial class ApplicationStatusHistory
{
    public int Id { get; set; }

    /// <summary>The status it moved from. 0 on the very first row, when the application was created.</summary>
    public long PreviousStatus { get; set; }

    public long NewStatus { get; set; }

    /// <summary>Usually optional, but the review rules require it for Return and Deny.</summary>
    public string? Comment { get; set; }

    /// <summary>Filled in when this change came from a property manager's review.</summary>
    public ReviewOutcome? Outcome { get; set; }

    public DateTime ChangedDate { get; set; }

    /// <summary>Identity user id of whoever made the change.</summary>
    public string ChangedByUser { get; set; } = null!;

    public int RentalApplicationId { get; set; }

    public virtual RentalApplication RentalApplication { get; set; } = null!;
}
