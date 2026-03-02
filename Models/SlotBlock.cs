using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class SlotBlock
{
    public int Id { get; set; }

    public int SlotId { get; set; }

    public DateOnly BlockDate { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual PickupSlot Slot { get; set; } = null!;
}
