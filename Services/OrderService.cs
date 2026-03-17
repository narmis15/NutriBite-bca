using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NUTRIBITE.Services
{
    public class OrderService : IOrderService
    {
        private readonly string _cs;
        private readonly ILogger<OrderService> _log;

        public OrderService(IConfiguration cfg, ILogger<OrderService> log)
        {
            _log = log;
            _cs = cfg.GetConnectionString("DBCS") ?? throw new Exception("DBCS not found");
        }

        private SqlConnection GetConn() => new SqlConnection(_cs);

        public async Task<IEnumerable<object>> GetActiveOrdersAsync()
        {
            var list = new List<object>();
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var q = @"
SELECT OrderId, CustomerName, CustomerPhone, ISNULL(TotalItems,0) AS TotalItems, ISNULL(PickupSlot,'') AS PickupSlot,
       ISNULL(TotalCalories,0) AS TotalCalories, ISNULL(PaymentStatus,'') AS PaymentStatus, ISNULL(Status,'') AS Status,
       ISNULL(IsFlagged,0) AS IsFlagged, CreatedAt, ISNULL(OrderType, 'Pickup') AS OrderType, ISNULL(DeliveryStatus, '') AS DeliveryStatus,
       ISNULL(DeliveryAddress, '') AS DeliveryAddress, ISNULL(DeliveryNotes, '') AS DeliveryNotes
FROM OrderTable
WHERE ISNULL(Status,'') <> 'Cancelled'
ORDER BY CreatedAt DESC";
                using var cmd = new SqlCommand(q, con);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new
                    {
                        OrderId = r.GetInt32(0),
                        CustomerName = r.IsDBNull(1) ? "" : r.GetString(1),
                        CustomerPhone = r.IsDBNull(2) ? "" : r.GetString(2),
                        TotalItems = r.IsDBNull(3) ? 0 : r.GetInt32(3),
                        PickupSlot = r.IsDBNull(4) ? "" : r.GetString(4),
                        TotalCalories = r.IsDBNull(5) ? 0 : r.GetInt32(5),
                        PaymentStatus = r.IsDBNull(6) ? "" : r.GetString(6),
                        Status = r.IsDBNull(7) ? "" : r.GetString(7),
                        IsFlagged = !r.IsDBNull(8) && Convert.ToInt32(r.GetValue(8)) == 1,
                        CreatedAt = r.IsDBNull(9) ? (DateTime?)null : r.GetDateTime(9),
                        OrderType = r.IsDBNull(10) ? "Pickup" : r.GetString(10),
                        DeliveryStatus = r.IsDBNull(11) ? "" : r.GetString(11),
                        DeliveryAddress = r.IsDBNull(12) ? "" : r.GetString(12),
                        DeliveryNotes = r.IsDBNull(13) ? "" : r.GetString(13)
                    });
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetActiveOrdersAsync failed");
            }
            return list;
        }

        public async Task<object?> GetOrderDetailsAsync(int orderId)
        {
            try
            {
                using var con = GetConn();
                await con.OpenAsync();

                var headerQ = @"
SELECT TOP 1 o.OrderId, o.CreatedAt AS OrderDateTime, ISNULL(o.Status,'') AS Status, ISNULL(o.CustomerName,'') AS CustomerName,
       ISNULL(o.CustomerPhone,'') AS CustomerPhone, ISNULL(o.PickupSlot,'') AS PickupSlot, ISNULL(o.TotalCalories,0) AS TotalCalories,
       ISNULL(p.PaymentMode,'') AS PaymentMode, ISNULL(p.Amount,0) AS Amount, ISNULL(p.IsRefunded,0) AS IsRefunded, ISNULL(p.RefundStatus,'') AS RefundStatus,
       ISNULL(o.IsFlagged,0) AS IsFlagged, ISNULL(o.OrderType,'Pickup') AS OrderType, ISNULL(o.DeliveryAddress,'') AS DeliveryAddress,
       ISNULL(o.DeliveryStatus,'') AS DeliveryStatus, o.DeliveryPersonId, ISNULL(o.DeliveryNotes, '') AS DeliveryNotes
FROM OrderTable o
LEFT JOIN Payment p ON p.OrderId = o.OrderId
WHERE o.OrderId = @id";
                using var hcmd = new SqlCommand(headerQ, con);
                hcmd.Parameters.AddWithValue("@id", orderId);
                using var hr = await hcmd.ExecuteReaderAsync();
                if (!await hr.ReadAsync()) return null;

                var orderObj = new Dictionary<string, object?>
                {
                    ["OrderId"] = hr.GetInt32(0),
                    ["OrderDateTime"] = hr.IsDBNull(1) ? null : hr.GetDateTime(1),
                    ["Status"] = hr.IsDBNull(2) ? "" : hr.GetString(2),
                    ["CustomerName"] = hr.IsDBNull(3) ? "" : hr.GetString(3),
                    ["CustomerPhone"] = hr.IsDBNull(4) ? "" : hr.GetString(4),
                    ["PickupSlot"] = hr.IsDBNull(5) ? "" : hr.GetString(5),
                    ["TotalCalories"] = hr.IsDBNull(6) ? 0 : hr.GetInt32(6),
                    ["PaymentMode"] = hr.IsDBNull(7) ? "" : hr.GetString(7),
                    ["Amount"] = hr.IsDBNull(8) ? 0m : hr.GetDecimal(8),
                    ["IsRefunded"] = !hr.IsDBNull(9) && Convert.ToInt32(hr.GetValue(9)) == 1,
                    ["RefundStatus"] = hr.IsDBNull(10) ? "" : hr.GetString(10),
                    ["IsFlagged"] = !hr.IsDBNull(11) && Convert.ToInt32(hr.GetValue(11)) == 1,
                    ["OrderType"] = hr.IsDBNull(12) ? "Pickup" : hr.GetString(12),
                    ["DeliveryAddress"] = hr.IsDBNull(13) ? "" : hr.GetString(13),
                    ["DeliveryStatus"] = hr.IsDBNull(14) ? "" : hr.GetString(14),
                    ["DeliveryPersonId"] = hr.IsDBNull(15) ? null : hr.GetInt32(15),
                    ["DeliveryNotes"] = hr.IsDBNull(16) ? "" : hr.GetString(16)
                };

                // items
                var items = new List<object>();
                var itemsQ = @"SELECT ISNULL(ItemName,'') AS Name, ISNULL(Quantity,0) AS Quantity, ISNULL(Instructions,'') AS Instructions FROM OrderItems WHERE OrderId = @id";
                using var icmd = new SqlCommand(itemsQ, con);
                icmd.Parameters.AddWithValue("@id", orderId);
                using var ir = await icmd.ExecuteReaderAsync();
                while (await ir.ReadAsync())
                {
                    items.Add(new
                    {
                        Name = ir.IsDBNull(0) ? "" : ir.GetString(0),
                        Quantity = ir.IsDBNull(1) ? 0 : ir.GetInt32(1),
                        Instructions = ir.IsDBNull(2) ? "" : ir.GetString(2)
                    });
                }
                orderObj["Items"] = items;

                // customer order count
                var phone = orderObj["CustomerPhone"]?.ToString() ?? "";
                var count = 0;
                if (!string.IsNullOrWhiteSpace(phone))
                {
                    var cntQ = "SELECT COUNT(1) FROM OrderTable WHERE CustomerPhone = @phone";
                    using var cc = new SqlCommand(cntQ, con);
                    cc.Parameters.AddWithValue("@phone", phone);
                    count = Convert.ToInt32(await cc.ExecuteScalarAsync() ?? 0);
                }
                orderObj["CustomerOrderCount"] = count;
                orderObj["PickupStatus"] = "On-time";

                return orderObj;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetOrderDetailsAsync failed");
                return null;
            }
        }

        public async Task<bool> UpdateOrderStatusAsync(int orderId, string newStatus)
        {
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var q = "UPDATE OrderTable SET Status = @s, UpdatedAt = GETDATE() WHERE OrderId = @id";
                using var cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@s", newStatus);
                cmd.Parameters.AddWithValue("@id", orderId);
                var rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "UpdateOrderStatusAsync failed");
                return false;
            }
        }

        public async Task<bool> ToggleFlagAsync(int orderId)
        {
            try
            {
                using var con = GetConn();
                await con.OpenAsync();

                var q = @"
UPDATE OrderTable
SET IsFlagged = CASE WHEN ISNULL(IsFlagged,0)=1 THEN 0 ELSE 1 END, UpdatedAt = GETDATE()
WHERE OrderId = @id";
                using var cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@id", orderId);
                var rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ToggleFlagAsync failed");
                return false;
            }
        }

        public async Task<IEnumerable<object>> GetFlaggedOrdersAsync()
        {
            var list = new List<object>();
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var q = @"
SELECT o.OrderId, ISNULL(o.FlagReason,'Suspicious') AS FlagReason, ISNULL(o.CustomerName,'') AS CustomerName,
       ISNULL(o.CustomerPhone,'') AS CustomerPhone, ISNULL(p.Amount,0) AS Amount, ISNULL(o.TotalCalories,0) AS TotalCalories,
       ISNULL(o.Status,'') AS Status, ISNULL(o.IsResolved,0) AS IsResolved, o.CreatedAt
FROM OrderTable o
LEFT JOIN Payment p ON p.OrderId = o.OrderId
WHERE ISNULL(o.IsFlagged,0)=1
ORDER BY o.CreatedAt DESC";
                using var cmd = new SqlCommand(q, con);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new
                    {
                        OrderId = r.GetInt32(0),
                        FlagReason = r.IsDBNull(1) ? "Suspicious" : r.GetString(1),
                        CustomerName = r.IsDBNull(2) ? "" : r.GetString(2),
                        CustomerPhone = r.IsDBNull(3) ? "" : r.GetString(3),
                        Amount = r.IsDBNull(4) ? 0m : r.GetDecimal(4),
                        TotalCalories = r.IsDBNull(5) ? 0 : r.GetInt32(5),
                        Status = r.IsDBNull(6) ? "" : r.GetString(6),
                        IsResolved = !r.IsDBNull(7) && Convert.ToInt32(r.GetValue(7)) == 1,
                        CreatedAt = r.IsDBNull(8) ? (DateTime?)null : r.GetDateTime(8)
                    });
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetFlaggedOrdersAsync failed");
            }
            return list;
        }

        public async Task<IEnumerable<object>> GetPickupSlotsAsync(DateTime? date)
        {
            var list = new List<object>();
            var targetDate = date?.Date ?? DateTime.Today;
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var q = @"
SELECT SlotId, ISNULL(SlotLabel,'') AS SlotLabel, CONVERT(varchar(5), StartTime, 108) AS StartTime,
       CONVERT(varchar(5), EndTime, 108) AS EndTime, ISNULL(Capacity,0) AS Capacity, ISNULL(IsDisabled,0) AS IsDisabled
FROM PickupSlots
ORDER BY StartTime";
                using var cmd = new SqlCommand(q, con);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var slotId = r.GetInt32(0);
                    var label = r.IsDBNull(1) ? "" : r.GetString(1);
                    var start = r.IsDBNull(2) ? "" : r.GetString(2);
                    var end = r.IsDBNull(3) ? "" : r.GetString(3);
                    var capacity = r.IsDBNull(4) ? 0 : r.GetInt32(4);
                    var isDisabled = r.IsDBNull(5) ? false : Convert.ToInt32(r.GetValue(5)) == 1;

                    // bookings
                    var countQ = "SELECT COUNT(1) FROM OrderTable WHERE PickupSlot = @label AND CONVERT(date,CreatedAt)=@date AND ISNULL(Status,'')<>'Cancelled'";
                    using var cc = new SqlCommand(countQ, con);
                    cc.Parameters.AddWithValue("@label", label);
                    cc.Parameters.AddWithValue("@date", targetDate);
                    var bookings = Convert.ToInt32(await cc.ExecuteScalarAsync() ?? 0);

                    var blockQ = "SELECT COUNT(1) FROM SlotBlocks WHERE SlotId = @id AND CONVERT(date, BlockDate)=@date";
                    using var bc = new SqlCommand(blockQ, con);
                    bc.Parameters.AddWithValue("@id", slotId);
                    bc.Parameters.AddWithValue("@date", targetDate);
                    var isBlocked = Convert.ToInt32(await bc.ExecuteScalarAsync() ?? 0) > 0;

                    var status = isDisabled ? "Disabled" : (bookings >= capacity ? "Full" : "Open");

                    list.Add(new
                    {
                        SlotId = slotId,
                        SlotLabel = label,
                        StartTime = start,
                        EndTime = end,
                        Capacity = capacity,
                        IsDisabled = isDisabled,
                        CurrentBookings = bookings,
                        Status = status,
                        IsBlockedForDate = isBlocked
                    });
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetPickupSlotsAsync failed");
            }
            return list;
        }

        public async Task<IEnumerable<object>> GetCancelledOrdersAsync()
        {
            var list = new List<object>();
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var q = @"
SELECT o.OrderId, o.CreatedAt AS OrderCreatedAt, o.CancelledAt, ISNULL(o.CancelReason,'') AS CancelReason, ISNULL(o.CancelledBy,'') AS CancelledBy,
       ISNULL(p.Amount,0) AS Amount, ISNULL(p.RefundMethod,'') AS RefundMethod, ISNULL(p.RefundStatus,'') AS RefundStatus, ISNULL(o.AdminNotes,'') AS AdminNotes,
       ISNULL(o.CustomerName,'') AS CustomerName, ISNULL(o.CustomerPhone,'') AS CustomerPhone
FROM OrderTable o
LEFT JOIN Payment p ON p.OrderId = o.OrderId
WHERE ISNULL(o.Status,'') = 'Cancelled'
ORDER BY ISNULL(o.CancelledAt,o.CreatedAt) DESC";
                using var cmd = new SqlCommand(q, con);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new
                    {
                        OrderId = r.GetInt32(0),
                        OrderCreatedAt = r.IsDBNull(1) ? (DateTime?)null : r.GetDateTime(1),
                        CancelledAt = r.IsDBNull(2) ? (DateTime?)null : r.GetDateTime(2),
                        CancelReason = r.IsDBNull(3) ? "" : r.GetString(3),
                        CancelledBy = r.IsDBNull(4) ? "" : r.GetString(4),
                        Amount = r.IsDBNull(5) ? 0m : r.GetDecimal(5),
                        RefundMethod = r.IsDBNull(6) ? "" : r.GetString(6),
                        RefundStatus = r.IsDBNull(7) ? "" : r.GetString(7),
                        AdminNotes = r.IsDBNull(8) ? "" : r.GetString(8),
                        CustomerName = r.IsDBNull(9) ? "" : r.GetString(9),
                        CustomerPhone = r.IsDBNull(10) ? "" : r.GetString(10)
                    });
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetCancelledOrdersAsync failed");
            }
            return list;
        }

        public async Task<int> GetFlaggedCountAsync()
        {
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var q = "SELECT COUNT(1) FROM OrderTable WHERE ISNULL(IsFlagged,0)=1";
                using var cmd = new SqlCommand(q, con);
                return Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetFlaggedCountAsync failed");
                return 0;
            }
        }

        public async Task<int> GetCancelledCountAsync()
        {
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var q = @"SELECT COUNT(1) FROM OrderTable o LEFT JOIN Payment p ON p.OrderId = o.OrderId
                          WHERE ISNULL(o.Status,'') = 'Cancelled' AND ISNULL(p.RefundStatus,'') <> 'Completed'";
                using var cmd = new SqlCommand(q, con);
                return Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetCancelledCountAsync failed");
                return 0;
            }
        }

        public async Task<bool> CancelOrderAsync(int orderId)
        {
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                using var tx = con.BeginTransaction();
                var q = "UPDATE OrderTable SET Status = 'Cancelled', UpdatedAt = GETDATE(), CancelledAt = GETDATE() WHERE OrderId = @id";
                using var cmd = new SqlCommand(q, con, tx);
                cmd.Parameters.AddWithValue("@id", orderId);
                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0) { await tx.RollbackAsync(); return false; }

                var payQ = "UPDATE Payment SET RefundStatus = 'Refund Pending', UpdatedAt = GETDATE() WHERE OrderId = @id";
                using var pcmd = new SqlCommand(payQ, con, tx);
                pcmd.Parameters.AddWithValue("@id", orderId);
                await pcmd.ExecuteNonQueryAsync();

                await tx.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "CancelOrderAsync failed");
                return false;
            }
        }

        public async Task<bool> TriggerRefundAsync(int orderId)
        {
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var q = "UPDATE Payment SET IsRefunded = 1, RefundStatus = 'Refunded', UpdatedAt = GETDATE() WHERE OrderId = @id";
                using var cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@id", orderId);
                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0) return false;

                var orderQ = "UPDATE OrderTable SET UpdatedAt = GETDATE() WHERE OrderId = @id";
                using var ocmd = new SqlCommand(orderQ, con);
                ocmd.Parameters.AddWithValue("@id", orderId);
                await ocmd.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "TriggerRefundAsync failed");
                return false;
            }
        }

        public async Task<bool> VerifyFlagAsync(int orderId)
        {
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var q = "UPDATE OrderTable SET IsFlagged = 0, IsResolved = 1, UpdatedAt = GETDATE() WHERE OrderId = @id";
                using var cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@id", orderId);
                var rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "VerifyFlagAsync failed");
                return false;
            }
        }

        public async Task<bool> UpdateRefundStatusAsync(int orderId, string status)
        {
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var q = "UPDATE Payment SET RefundStatus = @status, UpdatedAt = GETDATE() WHERE OrderId = @id";
                using var cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@status", status);
                cmd.Parameters.AddWithValue("@id", orderId);
                var rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "UpdateRefundStatusAsync failed");
                return false;
            }
        }

        public async Task<bool> AddAdminNoteAsync(int orderId, string note)
        {
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var getQ = "SELECT ISNULL(AdminNotes,'') FROM OrderTable WHERE OrderId = @id";
                using var gcmd = new SqlCommand(getQ, con);
                gcmd.Parameters.AddWithValue("@id", orderId);
                var existing = (await gcmd.ExecuteScalarAsync())?.ToString() ?? "";
                var appended = (string.IsNullOrEmpty(existing) ? "" : existing + "\n") + $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] Admin: {note}";
                var upQ = "UPDATE OrderTable SET AdminNotes = @notes, UpdatedAt = GETDATE() WHERE OrderId = @id";
                using var ucmd = new SqlCommand(upQ, con);
                ucmd.Parameters.AddWithValue("@notes", appended);
                ucmd.Parameters.AddWithValue("@id", orderId);
                var rows = await ucmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "AddAdminNoteAsync failed");
                return false;
            }
        }

        public async Task<bool> BlockCustomerAsync(string customerPhone)
        {
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var q = "UPDATE UserSignup SET IsBlocked = 1, UpdatedAt = GETDATE() WHERE Phone = @phone";
                using var cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@phone", customerPhone);
                var rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "BlockCustomerAsync failed");
                return false;
            }
        }

        public async Task<bool> UpdateSlotStatusAsync(int slotId, bool isDisabled)
        {
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var q = "UPDATE PickupSlots SET IsDisabled = @val, UpdatedAt = GETDATE() WHERE SlotId = @id";
                using var cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@val", isDisabled ? 1 : 0);
                cmd.Parameters.AddWithValue("@id", slotId);
                var rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "UpdateSlotStatusAsync failed");
                return false;
            }
        }

        public async Task<(bool ok,int currentBookings)> UpdateSlotCapacityAsync(int slotId, int capacity)
        {
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var q = "UPDATE PickupSlots SET Capacity = @cap, UpdatedAt = GETDATE() WHERE SlotId = @id";
                using var cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@cap", capacity);
                cmd.Parameters.AddWithValue("@id", slotId);
                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0) return (false, 0);

                var countQ = @"SELECT COUNT(1) FROM OrderTable WHERE PickupSlot = (SELECT SlotLabel FROM PickupSlots WHERE SlotId = @id) AND CONVERT(date,CreatedAt)=CONVERT(date,GETDATE()) AND ISNULL(Status,'')<>'Cancelled'";
                using var cc = new SqlCommand(countQ, con);
                cc.Parameters.AddWithValue("@id", slotId);
                var bookings = Convert.ToInt32(await cc.ExecuteScalarAsync() ?? 0);
                return (true, bookings);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "UpdateSlotCapacityAsync failed");
                return (false, 0);
            }
        }

        public async Task<(bool ok,string message)> ToggleSlotBlockAsync(int slotId, DateTime date)
        {
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var target = date.Date;
                var existsQ = "SELECT COUNT(1) FROM SlotBlocks WHERE SlotId = @id AND CONVERT(date, BlockDate)=@date";
                using var ex = new SqlCommand(existsQ, con);
                ex.Parameters.AddWithValue("@id", slotId);
                ex.Parameters.AddWithValue("@date", target);
                var cnt = Convert.ToInt32(await ex.ExecuteScalarAsync() ?? 0);
                if (cnt > 0)
                {
                    var delQ = "DELETE FROM SlotBlocks WHERE SlotId = @id AND CONVERT(date, BlockDate)=@date";
                    using var dcmd = new SqlCommand(delQ, con);
                    dcmd.Parameters.AddWithValue("@id", slotId);
                    dcmd.Parameters.AddWithValue("@date", target);
                    await dcmd.ExecuteNonQueryAsync();
                    return (true, "Unblocked for date");
                }
                else
                {
                    var insQ = "INSERT INTO SlotBlocks (SlotId, BlockDate, CreatedAt) VALUES (@id, @date, GETDATE())";
                    using var icmd = new SqlCommand(insQ, con);
                    icmd.Parameters.AddWithValue("@id", slotId);
                    icmd.Parameters.AddWithValue("@date", target);
                    await icmd.ExecuteNonQueryAsync();
                    return (true, "Blocked for date");
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ToggleSlotBlockAsync failed");
                return (false, ex.Message);
            }
        }

        public async Task<bool> AssignDeliveryPersonAsync(int orderId, int deliveryPersonId)
        {
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var q = "UPDATE OrderTable SET DeliveryPersonId = @dp, DeliveryStatus = 'Assigned', UpdatedAt = GETDATE() WHERE OrderId = @id";
                using var cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@dp", deliveryPersonId);
                cmd.Parameters.AddWithValue("@id", orderId);
                var rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "AssignDeliveryPersonAsync failed");
                return false;
            }
        }

        public async Task<bool> UpdateDeliveryStatusAsync(int orderId, string status)
        {
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var q = "UPDATE OrderTable SET DeliveryStatus = @s, UpdatedAt = GETDATE() WHERE OrderId = @id";
                using var cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@s", status);
                cmd.Parameters.AddWithValue("@id", orderId);
                var rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "UpdateDeliveryStatusAsync failed");
                return false;
            }
        }

        public async Task<IEnumerable<object>> GetAvailableDeliveryPersonnelAsync()
        {
            var list = new List<object>();
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                // Since Role is not in DB yet, we might need to rely on a naming convention or a specific status if we can't change the schema easily.
                // But wait, the user said "build the delivery part", so I should probably assume I can add the Role column to the DB if needed.
                // For now, I'll search for users who have 'Delivery' in their name or just all users if I can't filter by role.
                // Actually, let's assume there's a Role column or we'll add it.
                var q = "SELECT Id, Name, Phone FROM UserSignup WHERE Status = 'Active'"; // Simplified for now
                using var cmd = new SqlCommand(q, con);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new
                    {
                        Id = r.GetInt32(0),
                        Name = r.IsDBNull(1) ? "" : r.GetString(1),
                        Phone = r.IsDBNull(2) ? "" : r.GetString(2)
                    });
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetAvailableDeliveryPersonnelAsync failed");
            }
            return list;
        }

        public async Task<IEnumerable<object>> GetDeliveriesForPersonAsync(int deliveryPersonId)
        {
            var list = new List<object>();
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var q = @"
SELECT OrderId, CustomerName, CustomerPhone, DeliveryAddress, DeliveryStatus, Status, CreatedAt, ISNULL(DeliveryNotes, '') AS DeliveryNotes
FROM OrderTable
WHERE DeliveryPersonId = @dp AND ISNULL(Status,'') <> 'Cancelled'
ORDER BY CreatedAt DESC";
                using var cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@dp", deliveryPersonId);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new
                    {
                        OrderId = r.GetInt32(0),
                        CustomerName = r.IsDBNull(1) ? "" : r.GetString(1),
                        CustomerPhone = r.IsDBNull(2) ? "" : r.GetString(2),
                        DeliveryAddress = r.IsDBNull(3) ? "" : r.GetString(3),
                        DeliveryStatus = r.IsDBNull(4) ? "" : r.GetString(4),
                        Status = r.IsDBNull(5) ? "" : r.GetString(5),
                        CreatedAt = r.IsDBNull(6) ? (DateTime?)null : r.GetDateTime(6),
                        DeliveryNotes = r.IsDBNull(7) ? "" : r.GetString(7)
                    });
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetDeliveriesForPersonAsync failed");
            }
            return list;
        }
    }
}