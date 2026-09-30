namespace Troy_Web_Property_Manager.Models;

/// <summary>
/// Private notes property managers keep on an application. Applicants never see these.
/// </summary>
/// <remarks>
/// <para>It's its own table, and <see cref="RentalApplication"/> deliberately has no navigation property pointing
/// here. That way no query that loads an application for an applicant (an <c>Include</c>, a projection, anything)
/// can reach the notes by accident - the only way in is <c>ApplicationService.GetManagerNotesAsync</c>, which is
/// managers only.</para>
/// <para><see cref="Version"/> is a concurrency token set by the app on every save. The edit form sends back the
/// version it was loaded with, so if another manager saved in between, the save fails instead of quietly
/// overwriting their notes.</para>
/// </remarks>
public partial class ManagerNote
{
    /// <summary>Doubles as the primary key, so it's one row per application.</summary>
    public int RentalApplicationId { get; set; }

    public string Notes { get; set; } = "";

    /// <summary>Identity user id of the manager who saved last.</summary>
    public string UpdatedByUser { get; set; } = null!;

    public DateTime UpdatedDate { get; set; }

    public Guid Version { get; set; }

    public virtual RentalApplication RentalApplication { get; set; } = null!;
}
