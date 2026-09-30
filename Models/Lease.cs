using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

/// <summary>
/// A lease. Gets created when a property manager approves an application (2.d) - a start date and a twelve-month
/// term. <see cref="EndDate"/> is exclusive (see <c>Rules/LeaseRules</c>), so the next lease can start the same day
/// the old one ends. If a lease covers today the unit isn't available; we figure that out from these dates rather
/// than storing it.
/// </summary>
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
