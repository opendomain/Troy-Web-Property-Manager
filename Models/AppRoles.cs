namespace Troy_Web_Property_Manager.Models;

/// <summary>
/// Our two roles - that's all there is. Identity keeps roles in AspNetRoles and who has what in AspNetUserRoles;
/// Program.SeedRoles creates them on startup.
/// </summary>
/// <remarks>
/// They're constants so <c>[Authorize(Roles = ...)]</c>, <c>User.IsInRole(...)</c>, the sign-up page and the seeder
/// all use the exact same strings. A typo turns into a compile error instead of a sneaky permissions bug.
/// </remarks>
public static class AppRoles
{
    public const string Applicant = "Applicant";
    public const string PropertyManager = "Property Manager";

    /// <summary>All the roles. Seeded on startup, shown at sign-up, and used to reject anything else that gets posted.</summary>
    public static readonly string[] All = [Applicant, PropertyManager];
}
