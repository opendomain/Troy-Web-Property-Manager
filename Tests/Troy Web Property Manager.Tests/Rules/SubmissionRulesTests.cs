using Troy_Web_Property_Manager.Rules;

namespace Troy_Web_Property_Manager.Tests.Rules
{
    public class SubmissionRulesTests
    {
        [Fact]
        public void CompleteApplication_HasNoBlockers()
        {
            Assert.Empty(SubmissionRules.GetBlockers(true, true, residenceCount: 1, unitHasActiveLease: false));
        }

        [Fact]
        public void NothingSaved_ListsBothSectionsInOrder()
        {
            Assert.Equal(
                [SubmissionRules.ApplicantInformationNotSaved, SubmissionRules.ResidenceHistoryNotSaved],
                SubmissionRules.GetBlockers(false, false, residenceCount: 0, unitHasActiveLease: false));
        }

        [Fact]
        public void SavedHistoryWithNoResidences_IsBlocked()
        {
            Assert.Equal([SubmissionRules.NoResidences], SubmissionRules.GetBlockers(true, true, residenceCount: 0, unitHasActiveLease: false));
        }

        [Fact]
        public void LeasedUnit_IsBlockedEvenWhenEverythingIsSaved()
        {
            Assert.Equal([SubmissionRules.UnitLeased], SubmissionRules.GetBlockers(true, true, residenceCount: 2, unitHasActiveLease: true));
        }
    }
}
