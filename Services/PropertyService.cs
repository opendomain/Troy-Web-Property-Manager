using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Troy_Web_Property_Manager.Data;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Rules;
using Troy_Web_Property_Manager.ViewModels;

namespace Troy_Web_Property_Manager.Services
{
    public class PropertyService(ApplicationDbContext db)
    {
        public Task<List<PropertyViewModel>> GetPropertiesAsync()
        {
            var leasedToday = LeaseRules.ActiveOn(DateTime.Today);
            return db.Properties.AsNoTracking().OrderBy(p => p.Name).Select(p => new PropertyViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Address = p.Address,
                Units = p.Units.OrderBy(u => u.UnitNumber).Select(u => new UnitViewModel
                {
                    Id = u.Id,
                    UnitNumber = u.UnitNumber,
                    Bedrooms = u.Bedrooms,
                    MonthlyRent = u.MonthlyRent,
                    UnitTypeName = u.UnitType.Name,
                    UnitTypeIsActive = u.UnitType.IsActive,
                    IsLeased = u.Leases.AsQueryable().Any(leasedToday)
                }).ToList()
            }).ToListAsync();
        }

        /// <summary>Units with no lease covering today, optionally for one property (filtered in SQL).</summary>
        public Task<List<AvailableUnitViewModel>> GetAvailableUnitsAsync(int? propertyId = null)
        {
            var leasedToday = LeaseRules.ActiveOn(DateTime.Today);
            return db.Units.AsNoTracking()
            .Where(u => !u.Leases.AsQueryable().Any(leasedToday))
            .Where(u => propertyId == null || u.PropertyId == propertyId)
            .OrderBy(u => u.Property.Name).ThenBy(u => u.UnitNumber)
            .Select(u => new AvailableUnitViewModel
            {
                Id = u.Id,
                PropertyName = u.Property.Name,
                UnitNumber = u.UnitNumber,
                Bedrooms = u.Bedrooms,
                MonthlyRent = u.MonthlyRent,
                UnitTypeName = u.UnitType.Name
            }).ToListAsync();
        }

        public Task<PropertyFormViewModel?> GetPropertyFormAsync(int id) =>
        db.Properties.Where(p => p.Id == id)
        .Select(p => new PropertyFormViewModel { Id = p.Id, Name = p.Name, Address = p.Address })
        .FirstOrDefaultAsync();
        public async Task<ServiceResult> SavePropertyAsync(PropertyFormViewModel model)
        {
            var property = model.Id is null ? new Property() : await db.Properties.FindAsync(model.Id);
            if (property is null) return ServiceResult.Missing();
            property.Name = model.Name!.Trim();
            property.Address = model.Address!.Trim();
            if (model.Id is null) db.Properties.Add(property);
            await db.SaveChangesAsync();
            return ServiceResult.Ok(property.Id);
        }
        public async Task<ServiceResult> DeletePropertyAsync(int id)
        {
            var property = await db.Properties.Include(p => p.Units).FirstOrDefaultAsync(p => p.Id == id);
            if (property is null) return ServiceResult.Missing();
            if (await db.RentalApplications.AnyAsync(a => a.Unit.PropertyId == id))
            {
                return ServiceResult.Error("This property has units with applications, so it can't be removed.");
            }
            // The Unit -> Property FK doesn't cascade, so the units are removed explicitly.
            // No applications means no leases either, since a lease is only created by approving an application.
            db.Units.RemoveRange(property.Units);
            db.Properties.Remove(property);
            await db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        /// <summary>Property dropdown options, with <paramref name="selectedId"/> preselected.</summary>
        public Task<List<SelectListItem>> GetPropertyOptionsAsync(int? selectedId = null) =>
            db.Properties.AsNoTracking().OrderBy(p => p.Name)
            .Select(p => new SelectListItem(p.Name, p.Id.ToString(), p.Id == selectedId))
            .ToListAsync();

        public Task<bool> PropertyExistsAsync(int id) => db.Properties.AnyAsync(p => p.Id == id);

        public Task<UnitFormViewModel?> GetUnitFormAsync(int id) =>
        db.Units.Where(u => u.Id == id).Select(u => new UnitFormViewModel
        {
            Id = u.Id,
            PropertyId = u.PropertyId,
            UnitNumber = u.UnitNumber,
            Bedrooms = u.Bedrooms,
            MonthlyRent = u.MonthlyRent,
            UnitTypeId = u.UnitTypeId
        }).FirstOrDefaultAsync();
        public async Task<ServiceResult> SaveUnitAsync(UnitFormViewModel model)
        {
            var unit = model.Id is null ? new Unit { PropertyId = model.PropertyId } : await
            db.Units.FindAsync(model.Id);
            if (unit is null || !await db.Properties.AnyAsync(p => p.Id == unit.PropertyId)) return
            ServiceResult.Missing();
            // Enforced on the server: an inactive type may stay on a unit that already has it, but can't be chosen otherwise.
            UnitType? type = await db.UnitTypes.FindAsync(model.UnitTypeId);
            if (type is null || !UnitTypeRules.CanAssign(type, model.Id is null ? null : unit.UnitTypeId))
            {
                return ServiceResult.Error("Choose an active unit type.", nameof(model.UnitTypeId));
            }
            var number = model.UnitNumber!.Trim();
            if (await db.Units.AnyAsync(u => u.PropertyId == unit.PropertyId && u.UnitNumber == number && u.Id != unit.Id))
            {
                return ServiceResult.Error("This unit number already exists at the property.",
                nameof(model.UnitNumber));
            }
            unit.UnitNumber = number;
            unit.Bedrooms = model.Bedrooms!.Value;
            unit.MonthlyRent = model.MonthlyRent!.Value;
            unit.UnitTypeId = type.Id;
            if (model.Id is null) db.Units.Add(unit);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (SqlErrors.IsUniqueViolation(ex))
            {
                // Another save took the same number between the check above and this insert/update.
                return ServiceResult.Error("This unit number already exists at the property.", nameof(model.UnitNumber));
            }
            return ServiceResult.Ok(unit.Id);
        }
        public async Task<ServiceResult> DeleteUnitAsync(int id)
        {
            var unit = await db.Units.FindAsync(id);
            if (unit is null) return ServiceResult.Missing();
            if (await db.RentalApplications.AnyAsync(a => a.UnitId == id))
            {
                return ServiceResult.Error("This unit has applications, so it can't be removed.");
            }
            db.Units.Remove(unit);
            await db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        /// <summary>Dropdown options: active types, plus the unit's current type when editing even if it is inactive.</summary>
        public Task<List<SelectListItem>> GetUnitTypeOptionsAsync(int? currentUnitTypeId) =>
            db.UnitTypes.Where(t => t.IsActive || t.Id == currentUnitTypeId).OrderBy(t => t.Name)
            .Select(t => new SelectListItem(t.IsActive ? t.Name : t.Name + " (inactive)", t.Id.ToString()))
            .ToListAsync();
    }
}
