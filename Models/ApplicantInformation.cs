namespace Troy_Web_Property_Manager.Models;

/// <summary>
/// The Applicant Information section of one application (name, phone, email, current address).
/// Each application has its own copy, so once it's submitted it doesn't change behind our backs.
/// </summary>
public partial class ApplicantInformation
{
    /// <summary>Doubles as the primary key, so it's one row per application.</summary>
    public int RentalApplicationId { get; set; }

    public string Name { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string CurrentAddress { get; set; } = null!;

    public virtual RentalApplication RentalApplication { get; set; } = null!;
}
