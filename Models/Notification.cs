using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class Notification
{
    public int NotificationId { get; set; }

    public string Message { get; set; } = null!;

    public string ReceiverType { get; set; } = null!;

    public int ReceiverId { get; set; }

    public DateTime Date { get; set; }
}
