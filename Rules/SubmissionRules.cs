namespace Troy_Web_Property_Manager.Rules
{
    /// <summary>
    /// What's stopping an application from being submitted (4.b.ii, 4.e). The Summary shows this list so the applicant
    /// knows exactly what to fix, and <c>ApplicationService.SubmitAsync</c> refuses while it isn't empty - one list,
    /// so the page and the server can't disagree.
    /// </summary>
    public static class SubmissionRules
    {
        public const string ApplicantInformationNotSaved = "Applicant information hasn't been saved yet.";
        public const string ResidenceHistoryNotSaved = "Residence history hasn't been saved yet.";
        public const string NoResidences = "Add at least one prior residence.";
        public const string UnitLeased = "This unit has an active lease and is no longer available.";

        /// <summary>Everything currently blocking submission, in section order. Empty means it's good to go.</summary>
        public static List<string> GetBlockers(bool applicantInformationSaved, bool residenceHistorySaved, int residenceCount, bool unitHasActiveLease)
        {
            var blockers = new List<string>();
            if (!applicantInformationSaved) blockers.Add(ApplicantInformationNotSaved);
            if (!residenceHistorySaved) blockers.Add(ResidenceHistoryNotSaved);
            // Saved but empty shouldn't happen (removing the last residence unsaves the section), but don't rely on it.
            else if (residenceCount == 0) blockers.Add(NoResidences);
            if (unitHasActiveLease) blockers.Add(UnitLeased);
            return blockers;
        }
    }
}
