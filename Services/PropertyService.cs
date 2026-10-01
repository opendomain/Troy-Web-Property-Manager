using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Troy_Web_Property_Manager.Data;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Rules;
using Troy_Web_Property_Manager.ViewModels;

namespace Troy_Web_Property_Manager.Services
{
    /// <summary>
    /// Properties, units and the unit type lookup (2.b–2.d).
    /// </summary>
    /// <remarks>
    /// <para>The controllers already handle the role checks (<c>PropertiesController</c> is managers only,
    /// <c>UnitsController</c> is applicants only and just reads available units), so these methods don't need a
    /// <see cref="CurrentUser"/>.</para>
    /// <para>Every read uses <c>Select</c> to go straight into a view model, so SQL only returns the columns the page
    /// needs - nested collections like a property's units included, all in one query. Availability is worked out in
    /// the query from the lease dates using the shared <see cref="LeaseRules.ActiveOn"/> expression.</para>
    /// <para>Every add, edit and remove is logged, and so is every refusal with its reason. Only managers reach these
    /// (the controller is locked to the role), so the controller's request log already says who.</para>
    /// </remarks>
    public class PropertyService(ApplicationDbContext db, BusinessClock? clock = null, ILogger<PropertyService>? logger = null)
    {
        // "Today" in the business's time zone (see BusinessClock), for the leased/available checks.
        private readonly BusinessClock _clock = clock ?? BusinessClock.Local;

        // Tests that don't pass a logger get one that discards everything.
        private readonly ILogger<PropertyService> _logger = logger ?? NullLogger<PropertyService>.Instance;

        /// <summary>Data for the Properties page: every property, its units, and whether each one is leased today.</summary>
        public Task<List<PropertyViewModel>> GetPropertiesAsync()
        {
            return ProjectProperties(db.Properties).ToListAsync();
        }

        /// <summary>One property card's worth of data, so a modal save can redraw just that card. Null if it's gone.</summary>
        public Task<PropertyViewModel?> GetPropertyAsync(int id)
        {
            // Filter before projecting so the WHERE runs in SQL.
            return ProjectProperties(db.Properties.Where(p => p.Id == id)).FirstOrDefaultAsync();
        }

        private IQueryable<PropertyViewModel> ProjectProperties(IQueryable<Property> properties)
        {
            // This is an Expression<Func<Lease, bool>> so EF can turn it into SQL. The AsQueryable() below is a trick
            // that lets u.Leases take an expression instead of a compiled delegate.
            var leasedToday = LeaseRules.ActiveOn(_clock.Today);
            return properties.AsNoTracking().OrderBy(p => p.Name).Select(p => new PropertyViewModel
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
            });
        }

        /// <summary>
        /// Units with no lease covering today, optionally just for one property and/or a minimum number of bedrooms,
        /// sorted by <paramref name="sort"/> (all in SQL). We never store "available" - a stored flag would be wrong the
        /// day a lease ends (2.d).
        /// </summary>
        public Task<List<AvailableUnitViewModel>> GetAvailableUnitsAsync(int? propertyId = null, int? minBedrooms = null,
            UnitSortColumn sort = UnitSortColumn.Property, SortDirection dir = SortDirection.Asc)
        {
            var leasedToday = LeaseRules.ActiveOn(_clock.Today);
            var units = db.Units.AsNoTracking()
            .Where(u => !u.Leases.AsQueryable().Any(leasedToday))
            .Where(u => propertyId == null || u.PropertyId == propertyId)
            .Where(u => minBedrooms == null || u.Bedrooms >= minBedrooms);
            return SortUnits(units, sort, dir == SortDirection.Desc)
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

        /// <summary>
        /// The ORDER BY for available units. Ties (say, every 2-bedroom unit) fall back to property name, unit number
        /// and id, always ascending, so the order within a tie is the page's normal reading order and never changes
        /// between loads.
        /// </summary>
        private static IOrderedQueryable<Unit> SortUnits(IQueryable<Unit> units, UnitSortColumn sort, bool descending)
        {
            var sorted = sort switch
            {
                UnitSortColumn.Bedrooms => descending ? units.OrderByDescending(u => u.Bedrooms) : units.OrderBy(u => u.Bedrooms),
                UnitSortColumn.Rent => descending ? units.OrderByDescending(u => u.MonthlyRent) : units.OrderBy(u => u.MonthlyRent),
                UnitSortColumn.Type => descending ? units.OrderByDescending(u => u.UnitType.Name) : units.OrderBy(u => u.UnitType.Name),
                // The default: by property, then unit number, both in the chosen direction.
                _ => descending
                    ? units.OrderByDescending(u => u.Property.Name).ThenByDescending(u => u.UnitNumber)
                    : units.OrderBy(u => u.Property.Name).ThenBy(u => u.UnitNumber)
            };
            return sorted.ThenBy(u => u.Property.Name).ThenBy(u => u.UnitNumber).ThenBy(u => u.Id);
        }

        public Task<PropertyFormViewModel?> GetPropertyFormAsync(int id)
        {
            return db.Properties.Where(p => p.Id == id)
            .Select(p => new PropertyFormViewModel { Id = p.Id, Name = p.Name, Address = p.Address })
            .FirstOrDefaultAsync();
        }
        public async Task<ServiceResult> SavePropertyAsync(PropertyFormViewModel model)
        {
            var property = model.Id is null ? new Property() : await db.Properties.FindAsync(model.Id);
            if (property is null)
            {
                _logger.LogWarning("Property {PropertyId} wasn't saved: it doesn't exist.", model.Id);
                return ServiceResult.Missing();
            }
            property.Name = model.Name!.Trim();
            property.Address = model.Address!.Trim();
            if (model.Id is null) db.Properties.Add(property);
            await db.SaveChangesAsync();
            if (model.Id is null) _logger.LogInformation("Property {PropertyId} added.", property.Id);
            else _logger.LogInformation("Property {PropertyId} updated.", property.Id);
            return ServiceResult.Ok(property.Id);
        }
        /// <summary>
        /// Removes a property and its units - unless any unit has applications. We need to keep applications (and
        /// their leases and history), so we block the delete instead of cascading it.
        /// </summary>
        public async Task<ServiceResult> DeletePropertyAsync(int id)
        {
            var property = await db.Properties.Include(p => p.Units).FirstOrDefaultAsync(p => p.Id == id);
            if (property is null)
            {
                _logger.LogWarning("Property {PropertyId} wasn't removed: it doesn't exist.", id);
                return ServiceResult.Missing();
            }
            if (await db.RentalApplications.AnyAsync(a => a.Unit.PropertyId == id))
            {
                _logger.LogInformation("Property {PropertyId} wasn't removed: its units have applications.", id);
                return ServiceResult.Error("This property has units with applications, so it can't be removed.");
            }
            // The Unit -> Property FK doesn't cascade, so remove the units ourselves.
            // No applications means no leases either, since the only way to get a lease is an approved application.
            db.Units.RemoveRange(property.Units);
            db.Properties.Remove(property);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (SqlErrors.IsReferenceConflict(ex))
            {
                // Someone applied for one of its units between our check and this save; the FK stopped the delete.
                _logger.LogInformation("Property {PropertyId} wasn't removed: someone applied for one of its units while it was being removed.", id);
                db.ChangeTracker.Clear();
                return ServiceResult.Error("This property has units with applications, so it can't be removed.");
            }
            _logger.LogInformation("Property {PropertyId} removed, with its {UnitCount} units.", id, property.Units.Count);
            return ServiceResult.Ok();
        }

        /// <summary>Options for the property dropdown, with <paramref name="selectedId"/> already selected.</summary>
        public Task<List<SelectListItem>> GetPropertyOptionsAsync(int? selectedId = null)
        {
            return db.Properties.AsNoTracking().OrderBy(p => p.Name)
            .Select(p => new SelectListItem(p.Name, p.Id.ToString(), p.Id == selectedId))
            .ToListAsync();
        }

        public Task<bool> PropertyExistsAsync(int id)
        {
            return db.Properties.AnyAsync(p => p.Id == id);
        }

        public Task<UnitFormViewModel?> GetUnitFormAsync(int id)
        {
            return db.Units.Where(u => u.Id == id).Select(u => new UnitFormViewModel
            {
                Id = u.Id,
                PropertyId = u.PropertyId,
                UnitNumber = u.UnitNumber,
                Bedrooms = u.Bedrooms,
                MonthlyRent = u.MonthlyRent,
                UnitTypeId = u.UnitTypeId
            }).FirstOrDefaultAsync();
        }
        /// <summary>
        /// Adds or edits a unit. Handles the two rules that need the database: you can't pick an inactive unit type
        /// (2.c says enforce it on the server), and unit numbers have to be unique within a property.
        /// </summary>
        public async Task<ServiceResult> SaveUnitAsync(UnitFormViewModel model)
        {
            // When editing, the unit keeps its saved PropertyId. We only use the posted one for a new unit.
            var unit = model.Id is null ? new Unit { PropertyId = model.PropertyId } : await
            db.Units.FindAsync(model.Id);
            if (unit is null || !await db.Properties.AnyAsync(p => p.Id == unit.PropertyId))
            {
                _logger.LogWarning("Unit {UnitId} wasn't saved: it or property {PropertyId} doesn't exist.", model.Id, model.PropertyId);
                return ServiceResult.Missing();
            }
            // Server-side check: a unit can keep an inactive type it already has, but nobody can newly pick one.
            // We compare against the unit's saved type (unit.UnitTypeId), never anything that was posted.
            UnitType? type = await db.UnitTypes.FindAsync(model.UnitTypeId);
            if (type is null || !UnitTypeRules.CanAssign(type, model.Id is null ? null : unit.UnitTypeId))
            {
                // The form only offers types that are allowed, so this is an old page - or a hand-made post.
                _logger.LogWarning("Unit {UnitId} on property {PropertyId} wasn't saved: unit type {UnitTypeId} is inactive or doesn't exist.",
                    model.Id, unit.PropertyId, model.UnitTypeId);
                return ServiceResult.Error("Choose an active unit type.", nameof(model.UnitTypeId));
            }
            var number = model.UnitNumber!.Trim();
            // Nice error message first. The unique index on (PropertyId, UnitNumber) is the real safety net (caught below).
            if (await db.Units.AnyAsync(u => u.PropertyId == unit.PropertyId && u.UnitNumber == number && u.Id != unit.Id))
            {
                _logger.LogInformation("Unit {UnitNumber} wasn't saved: property {PropertyId} already has one.", number, unit.PropertyId);
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
                // Someone else grabbed the same number between our check and this save.
                _logger.LogInformation("Unit {UnitNumber} wasn't saved: someone else added it to property {PropertyId} at the same time.",
                    number, unit.PropertyId);
                db.ChangeTracker.Clear();
                return ServiceResult.Error("This unit number already exists at the property.", nameof(model.UnitNumber));
            }
            catch (DbUpdateException ex) when (model.Id is null && SqlErrors.IsReferenceConflict(ex))
            {
                // The property was removed between our check and this save; the FK stopped the insert.
                _logger.LogWarning("Unit {UnitNumber} wasn't saved: property {PropertyId} was removed at the same time.",
                    number, unit.PropertyId);
                db.ChangeTracker.Clear();
                return ServiceResult.Missing();
            }
            if (model.Id is null) _logger.LogInformation("Unit {UnitId} added to property {PropertyId}.", unit.Id, unit.PropertyId);
            else _logger.LogInformation("Unit {UnitId} on property {PropertyId} updated.", unit.Id, unit.PropertyId);
            return ServiceResult.Ok(unit.Id);
        }
        /// <summary>Removes a unit, unless it has applications (we keep those - see <see cref="DeletePropertyAsync"/>).</summary>
        public async Task<ServiceResult> DeleteUnitAsync(int id)
        {
            var unit = await db.Units.FindAsync(id);
            if (unit is null)
            {
                _logger.LogWarning("Unit {UnitId} wasn't removed: it doesn't exist.", id);
                return ServiceResult.Missing();
            }
            if (await db.RentalApplications.AnyAsync(a => a.UnitId == id))
            {
                _logger.LogInformation("Unit {UnitId} wasn't removed: it has applications.", id);
                return ServiceResult.Error("This unit has applications, so it can't be removed.");
            }
            db.Units.Remove(unit);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (SqlErrors.IsReferenceConflict(ex))
            {
                // Someone applied for it between our check and this save; the FK stopped the delete.
                _logger.LogInformation("Unit {UnitId} wasn't removed: someone applied for it while it was being removed.", id);
                db.ChangeTracker.Clear();
                return ServiceResult.Error("This unit has applications, so it can't be removed.");
            }
            _logger.LogInformation("Unit {UnitId} removed from property {PropertyId}.", id, unit.PropertyId);
            return ServiceResult.Ok();
        }

        /// <summary>
        /// Dropdown options: the active types, plus the unit's current type when editing even if it's inactive (2.c).
        /// That's the UI side of the rule - <see cref="SaveUnitAsync"/> is what actually enforces it.
        /// </summary>
        public Task<List<SelectListItem>> GetUnitTypeOptionsAsync(int? currentUnitTypeId)
        {
            return db.UnitTypes.Where(t => t.IsActive || t.Id == currentUnitTypeId).OrderBy(t => t.Name)
            .Select(t => new SelectListItem(t.IsActive ? t.Name : t.Name + " (inactive)", t.Id.ToString()))
            .ToListAsync();
        }
    }
}
