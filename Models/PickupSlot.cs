using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class PickupSlot
{
    public int SlotId { get; set; }

    public string? SlotLabel { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public int? Capacity { get; set; }

    public bool? IsDisabled { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<SlotBlock> SlotBlocks { get; set; } = new List<SlotBlock>();
}
