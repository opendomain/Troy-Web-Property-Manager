using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

public partial class UnitType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<Unit> Units { get; set; } = new List<Unit>();
}
