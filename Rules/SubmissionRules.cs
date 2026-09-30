namespace Troy_Web_Property_Manager.Rules
{
    /// <summary>
    /// What's stopping an application from being submitted (4.b.ii, 4.e). The Summary shows this list so the applicant
    /// knows exactly what to fix, and <c>ApplicationService.SubmitAsync</c> refuses while it isn't empty - one list,
    /// so the page and the server can't disagree.
    /// </summary>
    /// <remarks>
    /// Sections can be saved with errors, so "saved" isn't enough any more: every field error still on a saved section
    /// (from <see cref="SectionValidator"/>) is a blocker too, listed under its section.
    /// </remarks>
    public static class SubmissionRules
    {
        public const string ApplicantInformationNotSaved = "Applicant information hasn't been saved yet.";
        public const string ResidenceHistoryNotSaved = "Residence history hasn't been saved yet.";
        public const string NoResidences = "Add at least one prior residence.";
        public const string UnitLeased = "This unit has an active lease and is no longer available.";

        /// <summary>
        /// Everything currently blocking submission, in section order. Empty means it's good to go.
        /// </summary>
        /// <param name="applicantInformationErrors">Errors on the saved Applicant Information.</param>
        /// <param name="residenceErrors">Errors on the residences, already worded to say which residence.</param>
        public static List<string> GetBlockers(bool applicantInformationSaved, IEnumerable<string> applicantInformationErrors,
            bool residenceHistorySaved, int residenceCount, IEnumerable<string> residenceErrors, bool unitHasActiveLease)
        {
            var blockers = new List<string>();
            if (!applicantInformationSaved) blockers.Add(ApplicantInformationNotSaved);
            else blockers.AddRange(applicantInformationErrors.Select(e => $"Applicant information: {e}"));

            if (!residenceHistorySaved) blockers.Add(ResidenceHistoryNotSaved);
            else if (residenceCount == 0) blockers.Add(NoResidences);
            // Residences are saved one at a time from the modal, so their errors count even before the section is.
            blockers.AddRange(residenceErrors.Select(e => $"Residence history: {e}"));

            if (unitHasActiveLease) blockers.Add(UnitLeased);
            return blockers;
        }
    }
}
