namespace Troy_Web_Property_Manager.Models
{
    public enum ApplicationStatus
    {
        Draft = 1, Submitted = 2, Returned = 3, Approved = 4, Denied = 5, Withdrawn = 6
    }
    public enum ReviewOutcome { Approve = 1, Return = 2, Deny = 3 }

    /// <summary>The sections of the single-page application editor, in order.</summary>
    public enum ApplicationSection { ApplicantInformation = 1, ResidenceHistory = 2, Summary = 3 }
}
