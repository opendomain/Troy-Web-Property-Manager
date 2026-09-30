namespace Troy_Web_Property_Manager.Models;

/// <summary>
/// The Applicant Information section of one rental application (name, phone, email, current address).
/// Stored per application, so a submitted application keeps exactly what was submitted.
/// </summary>
public partial class ApplicantInformation
{
    /// <summary>Also the primary key: one row per application.</summary>
    public int RentalApplicationId { get; set; }

    public string Name { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string CurrentAddress { get; set; } = null!;

    public virtual RentalApplication RentalApplication { get; set; } = null!;
}
