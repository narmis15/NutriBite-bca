using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class ReviewUser
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string Message { get; set; } = null!;

    public DateTime Date { get; set; }
}
