namespace Troy_Web_Property_Manager.Models;

public static class AppRoles
{
    public const string Applicant = "Applicant";
    public const string PropertyManager = "Property Manager";

    public static readonly string[] All = [Applicant, PropertyManager];
}
