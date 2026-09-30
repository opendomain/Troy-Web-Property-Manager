using System;
using System.Collections.Generic;

namespace Troy_Web_Property_Manager.Models;

/// <summary>A property we manage. Property managers look after it and its units (2.b).</summary>
public partial class Property
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Address { get; set; } = null!;

    public virtual ICollection<Unit> Units { get; set; } = new List<Unit>();
}
