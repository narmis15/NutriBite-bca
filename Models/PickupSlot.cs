using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace NUTRIBITE.Models
{
    [Table("PickupSlots")]
    public class PickupSlot
    {
        public int SlotId { get; set; }
        public string? SlotLabel { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public int? Capacity { get; set; }
        public bool? IsDisabled { get; set; }
    }
}