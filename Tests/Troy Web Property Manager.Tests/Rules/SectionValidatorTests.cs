using System.ComponentModel.DataAnnotations;
using Troy_Web_Property_Manager.Rules;
using Troy_Web_Property_Manager.ViewModels;

namespace Troy_Web_Property_Manager.Tests.Rules
{
    public class SectionValidatorTests
    {
        private static ResidenceViewModel ValidResidence()
        {
            return new()
            {
                Address = "5 Elm St",
                LandlordName = "Pat Landlord",
                LandlordPhone = "518-555-0199",
                MoveInDate = new DateOnly(2020, 1, 1),
                MoveOutDate = new DateOnly(2024, 12, 31)
            };
        }

        [Fact]
        public void ValidSection_HasNoErrors()
        {
            Assert.Empty(SectionValidator.Validate(ValidResidence()));
            Assert.Empty(SectionValidator.Validate(new ApplicantInformationViewModel
            {
                Name = "Alex",
                Phone = "518-555-0100",
                Email = "alex@example.com",
                CurrentAddress = "9 Oak Ave"
            }));
        }

        [Fact]
        public void Errors_AreKeyedByField_WithThePrefix()
        {
            var errors = SectionValidator.Validate(new ApplicantInformationViewModel { Name = "Alex", Phone = "x", Email = "alex@example.com", CurrentAddress = "" },
                "ApplicantInformation.");

            Assert.Equal(new[] { "ApplicantInformation.CurrentAddress", "ApplicantInformation.Phone" }, errors.Select(e => e.Field).Order());
        }

        [Fact]
        public void Messages_UseTheDisplayName_AndRequiredComesFirst()
        {
            var residence = ValidResidence();
            residence.LandlordPhone = null;

            var error = Assert.Single(SectionValidator.Validate(residence));

            Assert.Equal("The Landlord phone field is required.", error.Message);
            Assert.False(error.PreventsSave);
        }

        [Fact]
        public void TwoFieldRule_RunsEvenWhenOtherFieldsFail()
        {
            // Validator.TryValidateObject would skip IValidatableObject here because Address is blank.
            var residence = ValidResidence();
            residence.Address = "";
            residence.MoveInDate = new DateOnly(2025, 1, 1);

            var fields = SectionValidator.Validate(residence).Select(e => e.Field).Order();

            Assert.Equal(new[] { nameof(ResidenceViewModel.Address), nameof(ResidenceViewModel.MoveOutDate) }, fields);
        }

        [Fact]
        public void TooLongForTheColumn_PreventsSave()
        {
            var residence = ValidResidence();
            residence.Address = new string('x', 51);

            var error = Assert.Single(SectionValidator.Validate(residence));

            Assert.Equal(nameof(ResidenceViewModel.Address), error.Field);
            Assert.True(error.PreventsSave);
        }

        /// <summary>A section whose object-level rule isn't tied to a field and has no message.</summary>
        private sealed class SectionWithAGeneralRule : IValidatableObject
        {
            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                yield return new ValidationResult(null);
            }
        }

        [Fact]
        public void RuleWithNoField_GoesInTheGeneralErrors()
        {
            var error = Assert.Single(SectionValidator.Validate(new SectionWithAGeneralRule(), prefix: "Section."));

            Assert.Equal("", error.Field);
            Assert.Equal("", error.Message);
        }
    }
}
