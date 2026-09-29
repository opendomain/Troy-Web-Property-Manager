using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

public partial class Status
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<RentApplication> RentApplications { get; set; } = new List<RentApplication>();
}
