using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

/// <summary>
/// Unit Type lookup, with Active and Inactive values (2.c). An inactive type still shows on units that already
/// have it, but nobody can pick it for another unit. The rule lives in <c>Rules/UnitTypeRules</c>.
/// </summary>
public partial class UnitType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<Unit> Units { get; set; } = new List<Unit>();
}
