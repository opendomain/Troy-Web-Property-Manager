using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

/// <summary>
/// The applicant as a person, tied to their login by <see cref="UserId"/>. <see cref="RentalApplications"/> are the ones
/// they started; <see cref="ApplicationApplicants"/> is every application they're on (those plus any they were added
/// to). Name, phone, email and address are just used to pre-fill a new application - each application keeps its own
/// copy in <see cref="ApplicantInformation"/>.
/// </summary>
public partial class Applicant
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string CurrentAddress { get; set; } = null!;

    /// <summary>The Identity user this profile belongs to.</summary>
    public string? UserId { get; set; }

    /// <summary>Applications this applicant started.</summary>
    public virtual ICollection<RentalApplication> RentalApplications { get; set; } = new List<RentalApplication>();

    /// <summary>Every application this applicant is on.</summary>
    public virtual ICollection<ApplicationApplicant> ApplicationApplicants { get; set; } = new List<ApplicationApplicant>();
}
