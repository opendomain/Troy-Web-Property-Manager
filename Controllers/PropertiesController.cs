using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;
using Troy_Web_Property_Manager.ViewModels;

namespace Troy_Web_Property_Manager.Controllers
{
    /// <summary>
    /// Where property managers add, edit and remove properties and units - all through modals (2.b).
    /// </summary>
    /// <remarks>
    /// <para>The whole controller is locked to the Property Manager role, so the actions don't need their own checks.
    /// An applicant who guesses a URL just gets a 403.</para>
    /// <para>Each form works the same way (Technical 1.b): the GET returns a partial for the modal, and the POST
    /// returns either that same partial with a 422 (didn't validate, redraw it) or JSON (worked - close the modal and
    /// redraw part of the page). Editing a property or any of its units only redraws that property's card
    /// (<c>#property-{id}</c> from <see cref="Card"/>); adding or removing a property redraws the whole list
    /// (<c>#property-list</c> from <see cref="List"/>).</para>
    /// </remarks>
    [Authorize(Roles = AppRoles.PropertyManager)]
    public class PropertiesController(PropertyService properties) : AppController
    {
        public async Task<IActionResult> Index()
        {
            return View(await properties.GetPropertiesAsync());
        }
        /// <summary>Just the property list partial. site.js calls this to refresh that part of the page.</summary>
        public async Task<IActionResult> List()
        {
            return PartialView("_PropertyList", await
            properties.GetPropertiesAsync());
        }

        /// <summary>Just one property's card, for refreshing it after a property or unit edit.</summary>
        public async Task<IActionResult> Card(int id)
        {
            var property = await properties.GetPropertyAsync(id);
            return property is null ? NotFound() : PartialView("_PropertyCard", property);
        }
        // ----- Property: same form for add (no id) and edit (id) -----
        public async Task<IActionResult> Edit(int? id)
        {
            var model = id is null ? new PropertyFormViewModel() : await properties.GetPropertyFormAsync(id.Value);
            return model is null ? NotFound() : PartialView("_PropertyForm", model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(PropertyFormViewModel model)
        {
            // Didn't validate - send the partial back with the errors (422) and the modal stays open.
            if (!ModelState.IsValid) return ModalInvalid("_PropertyForm", model);
            var result = await properties.SavePropertyAsync(model);
            if (IsAccessFailure(result)) return Failure(result);
            if (!result.Succeeded) return ModalFailed("_PropertyForm", model, result);
            // A new property needs a new card in the list; an edit only changes its own card.
            return model.Id is null ? RefreshList() : RefreshCard(result.Id);
        }

        public async Task<IActionResult> Delete(int id)
        {
            if (!await properties.PropertyExistsAsync(id)) return NotFound();
            return PartialView("_Confirm", ConfirmDelete(id));
        }
        [HttpPost, ActionName(nameof(Delete))]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // The service won't delete it if any unit has applications (we keep those), and the modal says why.
            var result = await properties.DeletePropertyAsync(id);
            if (IsAccessFailure(result)) return Failure(result);
            if (result.Succeeded) return RefreshList();
            return ModalFailed("_Confirm", ConfirmDelete(id), result);
        }

        // ----- Unit -----
        public async Task<IActionResult> EditUnit(int? id, int propertyId)
        {
            if (id is null && !await properties.PropertyExistsAsync(propertyId)) return NotFound();
            var model = id is null ? new UnitFormViewModel { PropertyId = propertyId } : await
            properties.GetUnitFormAsync(id.Value);
            if (model is null) return NotFound();
            // Active types, plus this unit's current type even if it's been made inactive (2.c).
            model.UnitTypes = await properties.GetUnitTypeOptionsAsync(model.UnitTypeId);
            return PartialView("_UnitForm", model);
        }

        [HttpPost]
        public async Task<IActionResult> EditUnit(UnitFormViewModel model)
        {
            // Look the unit up as saved - its property (for the card refresh) and type (for the dropdown) come from
            // here, never from the post.
            var stored = model.Id is null ? null : await properties.GetUnitFormAsync(model.Id.Value);
            if (ModelState.IsValid)
            {
                // The service handles the rules that need the database: unit numbers are unique per property, and
                // you can't pick an inactive type (2.c says to enforce that on the server).
                var result = await properties.SaveUnitAsync(model);
                if (IsAccessFailure(result)) return Failure(result);
                // For a new unit the service has already checked the posted property exists.
                if (result.Succeeded) return RefreshCard(stored?.PropertyId ?? model.PropertyId);
                AddErrors(result);
            }

            // Rebuild the dropdown from the unit's saved type, not what was posted.
            // Otherwise a tampered post could sneak an inactive type into the list.
            model.UnitTypes = await properties.GetUnitTypeOptionsAsync(stored?.UnitTypeId);
            return ModalInvalid("_UnitForm", model);
        }

        public async Task<IActionResult> DeleteUnit(int id)
        {
            if (await properties.GetUnitFormAsync(id) is null) return NotFound();
            return PartialView("_Confirm", ConfirmDeleteUnit(id));
        }
        [HttpPost, ActionName(nameof(DeleteUnit))]
        public async Task<IActionResult> DeleteUnitConfirmed(int id)
        {
            // Grab the property before the unit is gone, so we know which card to redraw.
            var stored = await properties.GetUnitFormAsync(id);
            var result = await properties.DeleteUnitAsync(id);
            if (IsAccessFailure(result)) return Failure(result);
            if (result.Succeeded) return RefreshCard(stored!.PropertyId);
            return ModalFailed("_Confirm", ConfirmDeleteUnit(id), result);
        }
        /// <summary>Close the modal and redraw the whole property list (a property was added or removed).</summary>
        private IActionResult RefreshList()
        {
            return ModalSuccess("#property-list", Url.Action(nameof(List)));
        }

        /// <summary>Close the modal and redraw just one property's card. The id matches the one in _PropertyCard.</summary>
        private IActionResult RefreshCard(int propertyId)
        {
            return ModalSuccess($"#property-{propertyId}", Url.Action(nameof(Card), new { id = propertyId }));
        }
        private ConfirmViewModel ConfirmDelete(int id)
        {
            return new("Remove property", "Remove this property and all of its units?", Url.Action(nameof(Delete), new
            {
                id
            })!);
        }
        private ConfirmViewModel ConfirmDeleteUnit(int id)
        {
            return new("Remove unit", "Remove this unit?", Url.Action(nameof(DeleteUnit), new { id })!);
        }
    }
}
