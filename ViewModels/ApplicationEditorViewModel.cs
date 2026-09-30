using Microsoft.AspNetCore.Mvc.ModelBinding;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Rules;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>
    /// The ONE view model behind the application page. Only Section and the Applicant Information
    /// fields are posted back; everything marked [BindNever] is display data rebuilt on the server.
    /// </summary>
    public class ApplicationEditorViewModel
    {
        public int Id { get; set; }
        public ApplicationSection Section { get; set; }
        public ApplicantInformationViewModel ApplicantInformation { get; set; } = new();
        [BindNever] public ApplicationStatus Status { get; set; }
        [BindNever] public string UnitLabel { get; set; } = "";
        [BindNever] public List<ResidenceViewModel> Residences { get; set; } = [];
        [BindNever] public bool ApplicantInformationSaved { get; set; }
        [BindNever] public bool ResidenceHistorySaved { get; set; }
        [BindNever] public bool CanEdit { get; set; } // server-side decision
        [BindNever] public bool IsReadOnly { get; set; } // !CanEdit, or on the Summary
        [BindNever] public bool IsManager { get; set; }

        /// <summary>The reviewer's comment when the application was returned or denied, shown to the applicant.</summary>
        [BindNever] public string? ReviewComment { get; set; }
        public bool CanSubmit
        {
            get { return CanEdit && ApplicantInformationSaved && ResidenceHistorySaved; }
        }

        public bool CanWithdraw
        {
            get { return !IsManager && !ApplicationWorkflow.IsTerminal(Status); }
        }

        public bool CanReview
        {
            get { return IsManager && ApplicationWorkflow.CanReview(Status); }
        }
    }
}
