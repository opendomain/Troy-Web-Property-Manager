using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

public partial class RentalApplication
{
    public int Id { get; set; }

    public int UnitId { get; set; }

    public int Status { get; set; }

    public DateTime Created { get; set; }

    /// <summary>Null while the application is a draft.</summary>
    public DateTime? Submitted { get; set; }

    /// <summary>Set when the Applicant Information section has been saved as valid.</summary>
    public bool ApplicantInformationSaved { get; set; }

    /// <summary>Set when the Residence History section has been saved as valid.</summary>
    public bool ResidenceHistorySaved { get; set; }

    public int ApplicantId { get; set; }

    public virtual Applicant Applicant { get; set; } = null!;

    /// <summary>This application's Applicant Information section; null until the section is first saved.</summary>
    public virtual ApplicantInformation? ApplicantInformation { get; set; }

    public virtual ICollection<ApplicationStatusHistory> ApplicationStatusHistories { get; set; } = new List<ApplicationStatusHistory>();

    public virtual ICollection<Lease> Leases { get; set; } = new List<Lease>();

    public virtual ICollection<Residence> Residences { get; set; } = new List<Residence>();

    public virtual Status StatusNavigation { get; set; } = null!;

    public virtual Unit Unit { get; set; } = null!;
}
