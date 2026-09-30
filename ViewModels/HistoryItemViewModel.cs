using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.ViewModels
{
    public class HistoryItemViewModel
    {
        public DateTime ChangedAt { get; set; }
        public string ChangedBy { get; set; } = "";
        /// <summary>Null for the first entry, when the application was started.</summary>
        public ApplicationStatus? FromStatus { get; set; }
        public ApplicationStatus ToStatus { get; set; }
        public ReviewOutcome? Outcome { get; set; }
        public string? Comment { get; set; }
    }
}
