using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

/// <summary>
/// The applicant (the person), linked to their login by <see cref="UserId"/>; owns their rental applications.
/// Name, phone, email and address are only defaults that pre-fill a new application's Applicant Information;
/// each application keeps its own copy in <see cref="ApplicantInformation"/>.
/// </summary>
public partial class Applicant
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string CurrentAddress { get; set; } = null!;

    /// <summary>The Identity user this applicant profile belongs to.</summary>
    public string? UserId { get; set; }

    public virtual ICollection<RentalApplication> RentalApplications { get; set; } = new List<RentalApplication>();
}
