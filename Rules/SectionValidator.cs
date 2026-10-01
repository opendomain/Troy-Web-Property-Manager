using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Troy_Web_Property_Manager.Rules
{
    /// <summary>One validation problem, tied to the field it belongs to.</summary>
    /// <param name="Field">The field's ModelState key (e.g. "ApplicantInformation.Phone" or "MoveOutDate"), so the
    /// message shows up under that input.</param>
    /// <param name="Message">What's wrong, worded for the applicant.</param>
    /// <param name="PreventsSave">True when the value can't be stored at all (longer than its column). Everything else
    /// can be saved and fixed later; it just blocks Submit.</param>
    public sealed record FieldError(string Field, string Message, bool PreventsSave = false);

    /// <summary>
    /// Runs a section's rules and hands back every error, keyed by field. The rules themselves are the DataAnnotations
    /// (and <see cref="IValidatableObject"/> rule) on each section's view model, so they're written once and used for
    /// the browser messages, the save, the fields on the page, the Summary's blocker list and Submit.
    /// </summary>
    /// <remarks>
    /// <para>Why not just <see cref="Validator.TryValidateObject(object, ValidationContext, ICollection{ValidationResult}?, bool)"/>?
    /// It skips <see cref="IValidatableObject.Validate"/> whenever a property has an error, so "move-out before
    /// move-in" would stay hidden until an unrelated blank field was filled in. Sections can now be saved with errors,
    /// so we want the whole list at once.</para>
    /// <para>Like MVC's field messages, it reports the first failing rule per property, with
    /// <see cref="RequiredAttribute"/> checked first.</para>
    /// </remarks>
    public static class SectionValidator
    {
        public static List<FieldError> Validate(object model, string prefix = "")
        {
            var errors = new List<FieldError>();
            foreach (var property in model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var rules = property.GetCustomAttributes<ValidationAttribute>(true)
                    .OrderBy(a => a is RequiredAttribute ? 0 : 1)
                    .ToList();
                if (rules.Count == 0) continue;

                var context = new ValidationContext(model)
                {
                    MemberName = property.Name,
                    DisplayName = property.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? property.Name
                };
                var value = property.GetValue(model);
                foreach (var rule in rules)
                {
                    if (rule.GetValidationResult(value, context) is { ErrorMessage: { } message })
                    {
                        // A value longer than the column can't be stored, so that one error stops the save.
                        errors.Add(new FieldError(prefix + property.Name, message, rule is StringLengthAttribute or MaxLengthAttribute));
                        break;
                    }
                }
            }

            if (model is IValidatableObject validatable)
            {
                foreach (var result in validatable.Validate(new ValidationContext(model)))
                {
                    // A rule with no member goes in the section's general errors ("" = the validation summary).
                    var members = result.MemberNames.Any() ? result.MemberNames : [""];
                    errors.AddRange(members.Select(m => new FieldError(m == "" ? "" : prefix + m, result.ErrorMessage ?? "")));
                }
            }
            return errors;
        }
    }
}
