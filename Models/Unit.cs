using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

/// <summary>
/// A unit in a property (2.b): unit number (unique within the property), bedrooms, monthly rent and unit type.
/// You can't delete a unit that has applications - we keep that history.
/// </summary>
public partial class Unit
{
    public int Id { get; set; }

    public string UnitNumber { get; set; } = null!;

    public int Bedrooms { get; set; } = 1;

    public decimal MonthlyRent { get; set; }

    public int UnitTypeId { get; set; }

    public int PropertyId { get; set; }

    public virtual ICollection<Lease> Leases { get; set; } = new List<Lease>();

    public virtual Property Property { get; set; } = null!;

    public virtual ICollection<RentalApplication> RentalApplications { get; set; } = new List<RentalApplication>();

    public virtual UnitType UnitType { get; set; } = null!;
}
