namespace Troy_Web_Property_Manager.Models;

/// <summary>
/// One applicant on one application. Everyone listed here can view and edit the application, including the applicant
/// who started it (<see cref="RentalApplication.ApplicantId"/>), who always has a row too.
/// </summary>
/// <remarks>
/// It's a real entity rather than a hidden many-to-many join so we can record who added whom and when.
/// <c>ApplicationService.Visible</c> filters on these rows, so every ownership check covers all the applicants.
/// </remarks>
public partial class ApplicationApplicant
{
    public int RentalApplicationId { get; set; }

    public int ApplicantId { get; set; }

    public DateTime Added { get; set; }

    /// <summary>Identity user id of whoever added them (the starter adds themselves when they create it).</summary>
    public string AddedByUser { get; set; } = null!;

    public virtual RentalApplication RentalApplication { get; set; } = null!;

    public virtual Applicant Applicant { get; set; } = null!;
}
