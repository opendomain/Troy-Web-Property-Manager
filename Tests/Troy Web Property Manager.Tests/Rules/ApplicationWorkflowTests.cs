using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Rules;

namespace Troy_Web_Property_Manager.Tests.Rules
{
    public class ApplicationWorkflowTests
    {
        [Theory]
        [InlineData(ApplicationStatus.Draft, ApplicationStatus.Submitted)]
        [InlineData(ApplicationStatus.Draft, ApplicationStatus.Withdrawn)]
        [InlineData(ApplicationStatus.Returned, ApplicationStatus.Submitted)]
        [InlineData(ApplicationStatus.Returned, ApplicationStatus.Withdrawn)]
        [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Approved)]
        [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Returned)]
        [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Denied)]
        [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Withdrawn)]
        public void CanTransition_AllowsWorkflowTransitions(ApplicationStatus from, ApplicationStatus to) =>
            Assert.True(ApplicationWorkflow.CanTransition(from, to));

        [Theory]
        [InlineData(ApplicationStatus.Draft, ApplicationStatus.Approved)]
        [InlineData(ApplicationStatus.Draft, ApplicationStatus.Returned)]
        [InlineData(ApplicationStatus.Returned, ApplicationStatus.Approved)]
        [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Draft)]
        [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Submitted)]
        public void CanTransition_RejectsOtherTransitions(ApplicationStatus from, ApplicationStatus to) =>
            Assert.False(ApplicationWorkflow.CanTransition(from, to));

        [Theory]
        [InlineData(ApplicationStatus.Approved)]
        [InlineData(ApplicationStatus.Denied)]
        [InlineData(ApplicationStatus.Withdrawn)]
        public void TerminalStatuses_AllowNoTransitions(ApplicationStatus terminal)
        {
            Assert.True(ApplicationWorkflow.IsTerminal(terminal));
            Assert.All(Enum.GetValues<ApplicationStatus>(), to => Assert.False(ApplicationWorkflow.CanTransition(terminal, to)));
        }

        [Theory]
        [InlineData(ApplicationStatus.Draft, true)]
        [InlineData(ApplicationStatus.Returned, true)]
        [InlineData(ApplicationStatus.Submitted, false)]
        [InlineData(ApplicationStatus.Approved, false)]
        [InlineData(ApplicationStatus.Denied, false)]
        [InlineData(ApplicationStatus.Withdrawn, false)]
        public void IsEditable_OnlyDraftOrReturned(ApplicationStatus status, bool expected) =>
            Assert.Equal(expected, ApplicationWorkflow.IsEditable(status));

        [Fact]
        public void CanReview_OnlySubmitted() =>
            Assert.Equal(new[] { ApplicationStatus.Submitted }, Enum.GetValues<ApplicationStatus>().Where(ApplicationWorkflow.CanReview));

        [Theory]
        [InlineData(ReviewOutcome.Approve, ApplicationStatus.Approved)]
        [InlineData(ReviewOutcome.Return, ApplicationStatus.Returned)]
        [InlineData(ReviewOutcome.Deny, ApplicationStatus.Denied)]
        public void StatusFor_MapsOutcomeToStatus(ReviewOutcome outcome, ApplicationStatus expected) =>
            Assert.Equal(expected, ApplicationWorkflow.StatusFor(outcome));

        [Fact]
        public void StatusFor_RejectsUnknownOutcome() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => ApplicationWorkflow.StatusFor((ReviewOutcome)99));

        [Theory]
        [InlineData(ReviewOutcome.Approve, false)]
        [InlineData(ReviewOutcome.Return, true)]
        [InlineData(ReviewOutcome.Deny, true)]
        public void RequiresComment_ForReturnAndDeny(ReviewOutcome outcome, bool expected) =>
            Assert.Equal(expected, ApplicationWorkflow.RequiresComment(outcome));

        [Theory]
        [InlineData(ApplicationSection.ApplicantInformation, ApplicationSection.ResidenceHistory)]
        [InlineData(ApplicationSection.ResidenceHistory, ApplicationSection.Summary)]
        [InlineData(ApplicationSection.Summary, ApplicationSection.Summary)]
        public void Next_MovesForwardAndStopsAtSummary(ApplicationSection section, ApplicationSection expected) =>
            Assert.Equal(expected, ApplicationWorkflow.Next(section));

        [Theory]
        [InlineData(ApplicationSection.Summary, ApplicationSection.ResidenceHistory)]
        [InlineData(ApplicationSection.ResidenceHistory, ApplicationSection.ApplicantInformation)]
        [InlineData(ApplicationSection.ApplicantInformation, ApplicationSection.ApplicantInformation)]
        public void Previous_MovesBackAndStopsAtFirstSection(ApplicationSection section, ApplicationSection expected) =>
            Assert.Equal(expected, ApplicationWorkflow.Previous(section));
    }
}
