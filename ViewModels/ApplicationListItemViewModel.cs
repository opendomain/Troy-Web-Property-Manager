using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>
    /// One row in the application list (6.a). <c>ApplicationService.ListAsync</c> fills it with a <c>Select</c>, so
    /// SQL only sends back these columns instead of whole entities.
    /// </summary>
    public class ApplicationListItemViewModel
    {
        public int Id { get; set; }
        public string PropertyName { get; set; } = "";
        public string UnitNumber { get; set; } = "";
        public string Applicant { get; set; } = "";
        public ApplicationStatus Status { get; set; }
        public DateTime? SubmittedAt { get; set; }
    }
}
