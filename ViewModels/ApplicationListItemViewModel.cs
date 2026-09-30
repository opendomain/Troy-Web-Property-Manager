using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.ViewModels
{
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
