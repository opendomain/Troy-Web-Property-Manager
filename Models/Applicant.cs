using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

/// <summary>
/// The applicant as a person, tied to their login by <see cref="UserId"/>. Their rental applications hang off this.
/// Name, phone, email and address are just used to pre-fill a new application - each application keeps its own
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

    public virtual ICollection<RentalApplication> RentalApplications { get; set; } = new List<RentalApplication>();
}
