using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class Admin
{
    public string UserId { get; set; } = null!;

    public string Password { get; set; } = null!;

    public int Id { get; set; }
}
