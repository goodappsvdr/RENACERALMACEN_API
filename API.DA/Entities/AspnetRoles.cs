using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class AspnetRoles
{
    public Guid ApplicationId { get; set; }

    public Guid RoleId { get; set; }

    public string RoleName { get; set; } = null!;

    public string LoweredRoleName { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<AspnetUsers> User { get; set; } = new List<AspnetUsers>();
}
