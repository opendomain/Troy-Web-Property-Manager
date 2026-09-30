using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

/// <summary>
/// A rental application - one applicant applying for one unit (4.a). It has a status (5.b), its two sections
/// (<see cref="ApplicantInformation"/> and <see cref="Residences"/>), its status history, and a lease once approved.
/// </summary>
/// <remarks>
/// <see cref="Status"/> is an EF concurrency token (set up in ApplicationDbContext). Every UPDATE adds
/// "WHERE Status = &lt;what we read&gt;", so if two people change the status at once, one of them loses instead of
/// silently clobbering the other.
/// </remarks>
public partial class RentalApplication
{
    public int Id { get; set; }

    public int UnitId { get; set; }

    public long Status { get; set; }

    public DateTime Created { get; set; }

    /// <summary>Null while the application is a draft.</summary>
    public DateTime? Submitted { get; set; }

    /// <summary>Set once the Applicant Information section has been saved and passed validation.</summary>
    public bool ApplicantInformationSaved { get; set; }

    /// <summary>Set once the Residence History section has been saved and passed validation.</summary>
    public bool ResidenceHistorySaved { get; set; }

    public int ApplicantId { get; set; }

    public virtual Applicant Applicant { get; set; } = null!;

    /// <summary>The Applicant Information section. Null until they save it the first time.</summary>
    public virtual ApplicantInformation? ApplicantInformation { get; set; }

    public virtual ICollection<ApplicationStatusHistory> ApplicationStatusHistories { get; set; } = new List<ApplicationStatusHistory>();

    public virtual ICollection<Lease> Leases { get; set; } = new List<Lease>();

    public virtual ICollection<Residence> Residences { get; set; } = new List<Residence>();

    public virtual Status StatusNavigation { get; set; } = null!;

    public virtual Unit Unit { get; set; } = null!;
}
