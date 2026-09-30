using System.ComponentModel.DataAnnotations;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.ViewModels;

namespace Troy_Web_Property_Manager.Tests.ViewModels
{
    /// <summary>The validation rules that live on the view models (attributes and IValidatableObject).</summary>
    public class ViewModelValidationTests
    {
        /// <summary>Validates like MVC does: attributes first, then IValidatableObject if they all pass.</summary>
        private static List<ValidationResult> Validate(object model)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
            return results;
        }

        private static IEnumerable<string> ErrorFields(object model)
        {
            return Validate(model).SelectMany(r => r.MemberNames);
        }

        // ---------------- Review ----------------

        [Fact]
        public void Review_ApproveWithoutComment_IsValid()
        {
            Assert.Empty(Validate(new ReviewViewModel { Outcome = ReviewOutcome.Approve }));
        }

        [Theory]
        [InlineData(ReviewOutcome.Return, null)]
        [InlineData(ReviewOutcome.Deny, "")]
        [InlineData(ReviewOutcome.Deny, "   ")]
        public void Review_ReturnOrDenyWithoutComment_RequiresComment(ReviewOutcome outcome, string? comment)
        {
            Assert.Equal(new[] { nameof(ReviewViewModel.Comment) }, ErrorFields(new ReviewViewModel { Outcome = outcome, Comment = comment }));
        }

        [Fact]
        public void Review_DenyWithComment_IsValid()
        {
            Assert.Empty(Validate(new ReviewViewModel { Outcome = ReviewOutcome.Deny, Comment = "Income too low." }));
        }

        [Fact]
        public void Review_WithoutOutcome_IsInvalid()
        {
            Assert.Contains(nameof(ReviewViewModel.Outcome), ErrorFields(new ReviewViewModel()));
        }

        [Fact]
        public void Review_CommentOver500Characters_IsInvalid()
        {
            Assert.Contains(nameof(ReviewViewModel.Comment),
                ErrorFields(new ReviewViewModel { Outcome = ReviewOutcome.Approve, Comment = new string('x', 501) }));
        }

        // ---------------- Residence ----------------

        private static ResidenceViewModel Residence(DateOnly moveIn, DateOnly moveOut)
        {
            return new()
            {
                Address = "5 Elm St",
                LandlordName = "Pat Landlord",
                LandlordPhone = "518-555-0100",
                MoveInDate = moveIn,
                MoveOutDate = moveOut
            };
        }

        [Fact]
        public void Residence_MoveOutAfterMoveIn_IsValid()
        {
            Assert.Empty(Validate(Residence(new DateOnly(2020, 1, 1), new DateOnly(2023, 6, 30))));
        }

        [Fact]
        public void Residence_MoveOutSameDayAsMoveIn_IsValid()
        {
            Assert.Empty(Validate(Residence(new DateOnly(2020, 1, 1), new DateOnly(2020, 1, 1))));
        }

        [Fact]
        public void Residence_MoveOutBeforeMoveIn_IsInvalid()
        {
            Assert.Equal(new[] { nameof(ResidenceViewModel.MoveOutDate) },
                ErrorFields(Residence(new DateOnly(2023, 6, 30), new DateOnly(2020, 1, 1))));
        }

        [Fact]
        public void Residence_MissingFields_AreRequired()
        {
            var fields = ErrorFields(new ResidenceViewModel()).ToList();
            Assert.Contains(nameof(ResidenceViewModel.Address), fields);
            Assert.Contains(nameof(ResidenceViewModel.LandlordName), fields);
            Assert.Contains(nameof(ResidenceViewModel.LandlordPhone), fields);
            Assert.Contains(nameof(ResidenceViewModel.MoveInDate), fields);
            Assert.Contains(nameof(ResidenceViewModel.MoveOutDate), fields);
        }

        // ---------------- Applicant information ----------------

        [Fact]
        public void ApplicantInformation_Complete_IsValid()
        {
            Assert.Empty(Validate(new ApplicantInformationViewModel
            {
                Name = "Alex Applicant",
                Phone = "518-555-0100",
                Email = "alex@example.com",
                CurrentAddress = "9 Oak Ave"
            }));
        }

        [Fact]
        public void ApplicantInformation_BadEmailAndTooLongName_AreInvalid()
        {
            var fields = ErrorFields(new ApplicantInformationViewModel
            {
                Name = new string('x', 51),
                Phone = "518-555-0100",
                Email = "not-an-email",
                CurrentAddress = "9 Oak Ave"
            }).ToList();
            Assert.Contains(nameof(ApplicantInformationViewModel.Name), fields);
            Assert.Contains(nameof(ApplicantInformationViewModel.Email), fields);
        }
    }
}
