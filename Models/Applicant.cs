using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

public partial class Applicant
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string CurrentAddress { get; set; } = null!;

    public virtual ICollection<RentApplication> RentApplications { get; set; } = new List<RentApplication>();
}
