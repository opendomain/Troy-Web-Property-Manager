using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

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
