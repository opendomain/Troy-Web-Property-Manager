using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;
using Troy_Web_Property_Manager.ViewModels;

namespace Troy_Web_Property_Manager.Controllers
{
    /// <summary>Property managers maintain properties and their units.</summary>
    [Authorize(Roles = AppRoles.PropertyManager)]
    public class PropertiesController(PropertyService properties) : AppController
    {
        public async Task<IActionResult> Index() => View(await properties.GetPropertiesAsync());
        /// <summary>Returns the property list partial; the modal script calls it to refresh the page region.</summary>
        public async Task<IActionResult> List() => PartialView("_PropertyList", await
        properties.GetPropertiesAsync());
        // ----- Property: one form for add (no id) and edit (id) -----
        public async Task<IActionResult> Edit(int? id)
        {
            var model = id is null ? new PropertyFormViewModel() : await properties.GetPropertyFormAsync(id.Value);
            return model is null ? NotFound() : PartialView("_PropertyForm", model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(PropertyFormViewModel model)
        {
            if (!ModelState.IsValid) return ModalInvalid("_PropertyForm", model);
            var result = await properties.SavePropertyAsync(model);
            return result.NotFound ? NotFound() : RefreshList();
        }

        public IActionResult Delete(int id) => PartialView("_Confirm", ConfirmDelete(id));
        [HttpPost, ActionName(nameof(Delete))]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var result = await properties.DeletePropertyAsync(id);
            if (result.NotFound) return NotFound();
            if (result.Succeeded) return RefreshList();
            AddErrors(result);
            return ModalInvalid("_Confirm", ConfirmDelete(id));
        }

        // ----- Unit -----
        public async Task<IActionResult> EditUnit(int? id, int propertyId)
        {
            if (id is null && !await properties.PropertyExistsAsync(propertyId)) return NotFound();
            var model = id is null ? new UnitFormViewModel { PropertyId = propertyId } : await
            properties.GetUnitFormAsync(id.Value);
            if (model is null) return NotFound();
            model.UnitTypes = await properties.GetUnitTypeOptionsAsync(model.UnitTypeId);
            return PartialView("_UnitForm", model);
        }

        [HttpPost]
        public async Task<IActionResult> EditUnit(UnitFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await properties.SaveUnitAsync(model);
                if (result.NotFound) return NotFound();
                if (result.Succeeded) return RefreshList();
                AddErrors(result);
            } 
            
            // Rebuild the dropdown from the unit's STORED type, not the posted one.
            var stored = model.Id is null ? null : await properties.GetUnitFormAsync(model.Id.Value);
            model.UnitTypes = await properties.GetUnitTypeOptionsAsync(stored?.UnitTypeId);
            return ModalInvalid("_UnitForm", model);
        }

        public IActionResult DeleteUnit(int id) => PartialView("_Confirm", ConfirmDeleteUnit(id));
        [HttpPost, ActionName(nameof(DeleteUnit))]
        public async Task<IActionResult> DeleteUnitConfirmed(int id)
        {
            var result = await properties.DeleteUnitAsync(id);
            if (result.NotFound) return NotFound();
            if (result.Succeeded) return RefreshList();
            AddErrors(result);
            return ModalInvalid("_Confirm", ConfirmDeleteUnit(id));
        }
        private IActionResult RefreshList() => ModalSuccess("#property-list", Url.Action(nameof(List)));
        private ConfirmViewModel ConfirmDelete(int id) =>
            new("Remove property", "Remove this property and all of its units?", Url.Action(nameof(Delete), new
            {
                id
            })!);
        private ConfirmViewModel ConfirmDeleteUnit(int id) =>
            new("Remove unit", "Remove this unit?", Url.Action(nameof(DeleteUnit), new { id })!);
    }
}
