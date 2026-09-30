using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

/// <summary>
/// Lookup table for <see cref="ApplicationStatus"/>. The ids match the enum values (Program.SeedLookups fills it),
/// which lets RentalApplications.Status be a real foreign key and gives you a readable name in SQL.
/// </summary>
public partial class Status
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<RentalApplication> RentalApplications { get; set; } = new List<RentalApplication>();
}
