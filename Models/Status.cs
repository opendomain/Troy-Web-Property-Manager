using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

public partial class Status
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<RentalApplication> RentalApplications { get; set; } = new List<RentalApplication>();
}
