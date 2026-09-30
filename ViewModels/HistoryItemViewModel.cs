using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>
    /// One line in the managers' status history panel (5.c: who, when, comment). The <c>ApplicationHistory</c> view
    /// component renders these.
    /// </summary>
    public class HistoryItemViewModel
    {
        public DateTime ChangedAt { get; set; }
        public string ChangedBy { get; set; } = "";
        /// <summary>Null on the first entry, when the application was started.</summary>
        public ApplicationStatus? FromStatus { get; set; }
        public ApplicationStatus ToStatus { get; set; }
        public ReviewOutcome? Outcome { get; set; }
        public string? Comment { get; set; }
    }
}
