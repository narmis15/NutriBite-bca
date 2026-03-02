using System;
using System.Collections.Generic;

namespace NUTRIBITE.Models;

public partial class Stock
{
    public int StockId { get; set; }

    public int ProductId { get; set; }

    public int VendorId { get; set; }

    public int AvailableQuantity { get; set; }

    public DateTime LastUpdatedDate { get; set; }
}
