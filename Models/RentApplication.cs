using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

public partial class RentApplication
{
    public int Id { get; set; }

    public int UnitId { get; set; }

    public int Status { get; set; }

    public DateTime Created { get; set; }

    public DateTime Submitted { get; set; }

    public int ApplicantId { get; set; }

    public virtual Applicant Applicant { get; set; } = null!;

    public virtual ICollection<ApplicationStatusHistory> ApplicationStatusHistories { get; set; } = new List<ApplicationStatusHistory>();

    public virtual ICollection<Lease> Leases { get; set; } = new List<Lease>();

    public virtual ICollection<Residence> Residences { get; set; } = new List<Residence>();

    public virtual Status StatusNavigation { get; set; } = null!;

    public virtual Unit Unit { get; set; } = null!;
}
