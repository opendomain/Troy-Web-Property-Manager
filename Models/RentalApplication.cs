using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

/// <summary>
/// A rental application for one unit (4.a), by one or more applicants (<see cref="ApplicationApplicants"/>). It has a
/// status (5.b), its two sections (<see cref="ApplicantInformation"/> and <see cref="Residences"/>), its status history,
/// and a lease once approved.
/// </summary>
/// <remarks>
/// <see cref="Status"/> is an EF concurrency token (set up in ApplicationDbContext). Every UPDATE adds
/// "WHERE Status = &lt;what we read&gt;", so if two people change the status at once, one of them loses instead of
/// silently clobbering the other.
/// <see cref="ReviewerUser"/> is one too, so a review or release can't go through against a claim that was released
/// and picked up by another manager in between (the status would be Under Review both times).
/// <para>Any applicant on it can edit it, so two of them can be saving at once. Each section has its own version
/// (<see cref="ApplicantInformationVersion"/>, <see cref="ResidenceHistoryVersion"/>). A save swaps in a new version
/// only if it still matches the one the page was loaded with, so a second save to the same section is rejected as
/// stale. These are deliberately <i>not</i> EF concurrency tokens - a token is checked on every update of the row, so
/// a save to one section would trip over a save to the other. <c>ApplicationService</c> checks them itself, one
/// section at a time.</para>
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

    /// <summary>
    /// Identity user id of the property manager who claimed it from the review queue. Only set while it's
    /// Under Review, and cleared again when it's released or reviewed.
    /// </summary>
    public string? ReviewerUser { get; set; }

    /// <summary>When <see cref="ReviewerUser"/> claimed it. Set and cleared together with it.</summary>
    public DateTime? ReviewClaimed { get; set; }

    /// <summary>
    /// The applicant who started the application. They're also in <see cref="ApplicationApplicants"/> like everyone
    /// else on it, but can't be removed. The "one open application per applicant and unit" index is on this column.
    /// </summary>
    public int ApplicantId { get; set; }

    /// <summary>Changes every time Applicant Information is saved. See the remarks.</summary>
    public Guid ApplicantInformationVersion { get; set; }

    /// <summary>Changes every time Residence History (the section or any residence in it) is saved. See the remarks.</summary>
    public Guid ResidenceHistoryVersion { get; set; }

    public virtual Applicant Applicant { get; set; } = null!;

    /// <summary>The Applicant Information section. Null until they save it the first time.</summary>
    public virtual ApplicantInformation? ApplicantInformation { get; set; }

    /// <summary>Everyone on the application, the starter included. They can all view and edit it.</summary>
    public virtual ICollection<ApplicationApplicant> ApplicationApplicants { get; set; } = new List<ApplicationApplicant>();

    public virtual ICollection<ApplicationStatusHistory> ApplicationStatusHistories { get; set; } = new List<ApplicationStatusHistory>();

    public virtual ICollection<Lease> Leases { get; set; } = new List<Lease>();

    public virtual ICollection<Residence> Residences { get; set; } = new List<Residence>();

    public virtual Status StatusNavigation { get; set; } = null!;

    public virtual Unit Unit { get; set; } = null!;
}
