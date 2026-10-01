using Troy_Web_Property_Manager.Rules;

namespace Troy_Web_Property_Manager.Tests.Rules
{
    public class SubmissionRulesTests
    {
        private static List<string> Blockers(bool infoSaved = true, string[]? infoErrors = null, bool historySaved = true,
            int residences = 1, string[]? residenceErrors = null, bool leased = false)
        {
            return SubmissionRules.GetBlockers(infoSaved, infoErrors ?? [], historySaved, residences, residenceErrors ?? [], leased);
        }

        [Fact]
        public void BothSectionsSavedWithNoErrors_HasNoBlockers()
        {
            Assert.Empty(Blockers());
        }

        [Fact]
        public void NeitherSectionSaved_ListsBothInOrder()
        {
            Assert.Equal(
                [SubmissionRules.ApplicantInformationNotSaved, SubmissionRules.ResidenceHistoryNotSaved],
                Blockers(infoSaved: false, historySaved: false, residences: 0));
        }

        [Fact]
        public void SavedHistoryWithNoResidences_IsBlocked()
        {
            Assert.Equal([SubmissionRules.NoResidences], Blockers(residences: 0));
        }

        [Fact]
        public void LeasedUnit_IsBlocked()
        {
            Assert.Equal([SubmissionRules.UnitLeased], Blockers(residences: 2, leased: true));
        }

        [Fact]
        public void ErrorsOnSavedSections_AreEachABlocker_UnderTheirSection()
        {
            Assert.Equal(
                ["Applicant information: The Phone field is required.", "Residence history: 5 Elm St - The Move-in date field is required."],
                Blockers(infoErrors: ["The Phone field is required."], residenceErrors: ["5 Elm St - The Move-in date field is required."]));
        }

        [Fact]
        public void UnsavedApplicantInformation_OnlySaysNotSaved()
        {
            // Field errors only count once the section has been saved.
            Assert.Equal([SubmissionRules.ApplicantInformationNotSaved], Blockers(infoSaved: false, infoErrors: ["The Phone field is required."]));
        }

        [Fact]
        public void ResidenceErrors_CountEvenBeforeTheSectionIsSaved()
        {
            // Residences are saved one at a time from the modal, so their errors are real straight away.
            Assert.Equal(
                [SubmissionRules.ResidenceHistoryNotSaved, "Residence history: 5 Elm St - bad"],
                Blockers(historySaved: false, residenceErrors: ["5 Elm St - bad"]));
        }
    }
}
