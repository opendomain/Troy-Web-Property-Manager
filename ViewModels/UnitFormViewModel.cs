using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>
    /// The add/edit unit modal (2.b): unit number, bedrooms, monthly rent and unit type. Same form for both -
    /// <see cref="Id"/> is null when adding.
    /// </summary>
    /// <remarks>
    /// <para>The attributes handle required fields and ranges (browser and server). Two rules need the database, so
    /// <c>PropertyService.SaveUnitAsync</c> handles them: unit numbers are unique within a property, and you can't
    /// pick an inactive unit type (2.c). Those errors are keyed to <see cref="UnitNumber"/> / <see cref="UnitTypeId"/>
    /// so they show up under the right field.</para>
    /// <para>We don't trust the dropdown. <see cref="UnitTypes"/> is <c>[BindNever]</c> and rebuilt on the server every
    /// time the form is shown, and if someone edits the HTML to post an inactive type's id, the server still says no
    /// (2.c).</para>
    /// </remarks>
    public class UnitFormViewModel
    {
        /// <summary>Null when adding a unit.</summary>
        public int? Id { get; set; }

        /// <summary>
        /// Hidden field, only used when adding. When editing, the service sticks with the unit's saved property, so
        /// nobody can move a unit to another property by changing this.
        /// </summary>
        public int PropertyId { get; set; }

        [Required, StringLength(20), Display(Name = "Unit number")] public string? UnitNumber { get; set; }

        // 0 bedrooms is fine - that's a studio.
        [Required, Range(0, 10)] public int? Bedrooms { get; set; }

        // Range on a decimal needs the typeof(decimal) overload. The column is decimal(10,2).
        [Required, Range(typeof(decimal), "1", "100000"), Display(Name = "Monthly rent")] public decimal? MonthlyRent { get; set; }

        [Required, Display(Name = "Unit type")] public int? UnitTypeId { get; set; }

        /// <summary>
        /// Dropdown options: the active types, plus the unit's current type if it's inactive so it still shows on a
        /// unit that already has it (2.c). Built by <c>PropertyService.GetUnitTypeOptionsAsync</c>.
        /// </summary>
        [BindNever] public List<SelectListItem> UnitTypes { get; set; } = [];
    }
}
