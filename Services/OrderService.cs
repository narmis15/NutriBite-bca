using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using NUTRIBITE.Models;
using NUTRIBITE.ViewModels;

using Microsoft.AspNetCore.SignalR;
using NUTRIBITE.Hubs;
using System.Dynamic;

namespace NUTRIBITE.Services
{
    public class OrderService : IOrderService
    {
        private readonly string _cs;
        private readonly ILogger<OrderService> _log;
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<AnalyticsHub> _hubContext;

        public OrderService(IConfiguration cfg, ILogger<OrderService> log, ApplicationDbContext db, IHubContext<AnalyticsHub> hubContext)
        {
            _log = log;
            _db = db;
            _hubContext = hubContext;
            _cs = cfg.GetConnectionString("DBCS") ?? throw new Exception("DBCS not found");
        }

        public IQueryable<OrderTable> GetOrderQueryable()
        {
            return _db.OrderTables.AsQueryable();
        }

        private SqlConnection GetConn() => new SqlConnection(_cs);

        public async Task<IEnumerable<object>> GetActiveOrdersAsync(DateTime? from = null, DateTime? to = null)
        {
            var list = new List<object>();
            try
            {
                using var con = GetConn();
                await con.OpenAsync();

                string whereClause = "(CreatedAt >= DATEADD(day, -30, GETDATE()) OR Status IN ('Placed', 'New', 'Accepted', 'Ready for Delivery', 'In Transit', 'On the Way'))";
                
                if (from.HasValue && to.HasValue)
                {
                    // If user provides dates, we filter specifically by those dates and IGNORE the status-based filter
                    // to allow viewing "any time" data as requested.
                    whereClause = "CAST(CreatedAt AS DATE) BETWEEN @from AND @to";
                }

                var q = $@"
SELECT OrderId, CustomerName, CustomerPhone, ISNULL(TotalItems,0) AS TotalItems,
       ISNULL(TotalCalories,0) AS TotalCalories, ISNULL(PaymentStatus,'') AS PaymentStatus, ISNULL(Status,'') AS Status,
       ISNULL(IsFlagged,0) AS IsFlagged, CreatedAt, 'Delivery' AS OrderType, ISNULL(DeliveryStatus, '') AS DeliveryStatus,
       ISNULL(DeliveryAddress, '') AS DeliveryAddress, ISNULL(DeliveryNotes, '') AS DeliveryNotes, ISNULL(TotalAmount, 0) AS TotalAmount,
       DeliveryPersonId
FROM OrderTable
WHERE {whereClause}
ORDER BY CreatedAt DESC";

                using var cmd = new SqlCommand(q, con);
                if (from.HasValue && to.HasValue)
                {
                    cmd.Parameters.AddWithValue("@from", from.Value.Date);
                    cmd.Parameters.AddWithValue("@to", to.Value.Date);
                }

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new
                    {
                        orderId = r.GetInt32(0),
                        customerName = r.IsDBNull(1) ? "Guest" : r.GetString(1),
                        customerPhone = r.IsDBNull(2) ? "No Phone" : r.GetString(2),
                        totalItems = r.IsDBNull(3) ? 0 : r.GetInt32(3),
                        totalCalories = r.IsDBNull(4) ? 0 : r.GetInt32(4),
                        paymentStatus = r.IsDBNull(5) ? "Pending" : r.GetString(5),
                        status = r.IsDBNull(6) ? "New" : r.GetString(6),
                        isFlagged = !r.IsDBNull(7) && Convert.ToInt32(r.GetValue(7)) == 1,
                        createdAt = r.IsDBNull(8) ? (DateTime?)null : r.GetDateTime(8),
                        orderType = "Delivery",
                        deliveryStatus = r.IsDBNull(10) ? "Unassigned" : r.GetString(10),
                        deliveryAddress = r.IsDBNull(11) ? "No Address" : r.GetString(11),
                        deliveryNotes = r.IsDBNull(12) ? "" : r.GetString(12),
                        totalAmount = r.IsDBNull(13) ? 0m : r.GetDecimal(13),
                        deliveryPersonId = r.IsDBNull(14) ? (int?)null : r.GetInt32(14)
                    });
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetActiveOrdersAsync failed");
            }
            return list;
        }

        public async Task<OrderDetailsViewModel> GetOrderDetailsAsync(int orderId)
        {
            try
            {
                using var con = GetConn();
                await con.OpenAsync();

                var headerQ = @"
SELECT TOP 1 o.OrderId, o.CreatedAt AS OrderDateTime, ISNULL(o.Status,'') AS Status, ISNULL(o.CustomerName,'') AS CustomerName,
       ISNULL(o.CustomerPhone,'') AS CustomerPhone, ISNULL(o.TotalCalories,0) AS TotalCalories,
       ISNULL(p.PaymentMode,'') AS PaymentMode, ISNULL(o.TotalAmount,0) AS Amount, ISNULL(o.CommissionAmount,0) AS CommissionAmount, ISNULL(o.VendorAmount,0) AS VendorAmount,
       ISNULL(p.IsRefunded,0) AS IsRefunded, ISNULL(p.RefundStatus,'') AS RefundStatus,
       ISNULL(o.OrderType, 'Delivery') AS OrderType, ISNULL(o.DeliveryAddress,'') AS DeliveryAddress,
       ISNULL(o.DeliveryStatus,'') AS DeliveryStatus, o.DeliveryPersonId, ISNULL(o.DeliveryNotes, '') AS DeliveryNotes,
       ISNULL(p.TransactionId, '') AS TransactionId, ISNULL(o.TrackingProgress, 0) AS TrackingProgress, ISNULL(o.PickupSlot, ''),
       ISNULL(u.Name, '') AS DeliveryAgentName
FROM OrderTable o
LEFT JOIN Payment p ON p.OrderId = o.OrderId
LEFT JOIN UserSignup u ON o.DeliveryPersonId = u.Id
WHERE o.OrderId = @id";
                using var hcmd = new SqlCommand(headerQ, con);
                hcmd.Parameters.AddWithValue("@id", orderId);
                using var hr = await hcmd.ExecuteReaderAsync();
                if (!await hr.ReadAsync()) return null;

                var orderObj = new OrderDetailsViewModel
                {
                    OrderId = Convert.ToInt32(hr.GetValue(0)),
                    OrderDateTime = hr.IsDBNull(1) ? "" : Convert.ToDateTime(hr.GetValue(1)).ToString("g"),
                    Status = hr.IsDBNull(2) ? "" : Convert.ToString(hr.GetValue(2)),
                    CustomerName = hr.IsDBNull(3) ? "" : Convert.ToString(hr.GetValue(3)),
                    CustomerPhone = hr.IsDBNull(4) ? "" : Convert.ToString(hr.GetValue(4)),
                    TotalCalories = hr.IsDBNull(5) ? 0 : Convert.ToInt32(hr.GetValue(5)),
                    PaymentMode = hr.IsDBNull(6) ? "" : Convert.ToString(hr.GetValue(6)),
                    Amount = hr.IsDBNull(7) ? 0m : Convert.ToDecimal(hr.GetValue(7)),
                    CommissionAmount = hr.IsDBNull(8) ? 0m : Convert.ToDecimal(hr.GetValue(8)),
                    VendorAmount = hr.IsDBNull(9) ? 0m : Convert.ToDecimal(hr.GetValue(9)),
                    IsRefunded = !hr.IsDBNull(10) && Convert.ToBoolean(hr.GetValue(10)),
                    RefundStatus = hr.IsDBNull(11) ? "" : Convert.ToString(hr.GetValue(11)),
                    OrderType = hr.IsDBNull(12) ? "Delivery" : Convert.ToString(hr.GetValue(12)),
                    DeliveryAddress = hr.IsDBNull(13) ? "" : Convert.ToString(hr.GetValue(13)),
                    DeliveryStatus = hr.IsDBNull(14) ? "" : Convert.ToString(hr.GetValue(14)),
                    DeliveryPersonId = hr.IsDBNull(15) ? null : Convert.ToInt32(hr.GetValue(15)),
                    DeliveryNotes = hr.IsDBNull(16) ? "" : Convert.ToString(hr.GetValue(16)),
                    TransactionId = hr.IsDBNull(17) ? "" : Convert.ToString(hr.GetValue(17)),
                    TrackingProgress = hr.IsDBNull(18) ? 0 : Convert.ToInt32(hr.GetValue(18)),
                    PickupSlot = hr.IsDBNull(19) ? "" : Convert.ToString(hr.GetValue(19)),
                    DeliveryAgentName = hr.IsDBNull(20) ? "" : Convert.ToString(hr.GetValue(20))
                };

                // items
                var items = new List<OrderItemViewModel>();
                var itemsQ = @"SELECT ISNULL(oi.ItemName,'') AS Name, ISNULL(oi.Quantity,0) AS Quantity, ISNULL(oi.SpecialInstruction,'') AS Instructions, ISNULL(f.ImagePath,'') AS ImageUrl FROM OrderItems oi LEFT JOIN Foods f ON oi.FoodId = f.Id WHERE oi.OrderId = @id";
                using var icmd = new SqlCommand(itemsQ, con);
                icmd.Parameters.AddWithValue("@id", orderId);
                using var ir = await icmd.ExecuteReaderAsync();
                while (await ir.ReadAsync())
                {
                    items.Add(new OrderItemViewModel
                    {
                        Name = ir.IsDBNull(0) ? "" : Convert.ToString(ir.GetValue(0)),
                        Quantity = ir.IsDBNull(1) ? 0 : Convert.ToInt32(ir.GetValue(1)),
                        Instructions = ir.IsDBNull(2) ? "" : Convert.ToString(ir.GetValue(2)),
                        ImageUrl = ir.IsDBNull(3) ? "" : Convert.ToString(ir.GetValue(3))
                    });
                }
                orderObj.Items = items;

                // customer order count
                var phone = orderObj.CustomerPhone ?? "";
                var count = 0;
                if (!string.IsNullOrWhiteSpace(phone))
                {
                    var cntQ = "SELECT COUNT(1) FROM OrderTable WHERE CustomerPhone = @phone";
                    using var cc = new SqlCommand(cntQ, con);
                    cc.Parameters.AddWithValue("@phone", phone);
                    count = Convert.ToInt32(await cc.ExecuteScalarAsync() ?? 0);
                }
                orderObj.CustomerOrderCount = count;
                orderObj.PickupStatus = "On-time";

                return orderObj;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetOrderDetailsAsync failed");
                throw;
            }
        }

        public async Task<bool> UpdateOrderStatusAsync(int orderId, string newStatus)
        {
            try
            {
                using var con = GetConn();
                await con.OpenAsync();

                // Define tracking progress mapping
                int progress = 1; // Default for Placed
                string deliveryStatus = null;
                
                int? autoAssignedId = null;
                if (newStatus.Equals("Accepted", StringComparison.OrdinalIgnoreCase)) 
                {
                    progress = 2;
                    // AUTO-ASSIGN LOGIC: Find an available delivery partner
                    try
                    {
                        var availablePartner = _db.UserSignups
                            .Where(u => u.Role == "Delivery" && u.Status == "Active")
                            .ToList()
                            .FirstOrDefault(u => !_db.OrderTables.Any(o => o.DeliveryPersonId == u.Id && 
                                                                         o.Status != "Completed" && 
                                                                         o.Status != "Delivered" && 
                                                                         o.Status != "Cancelled"));

                        if (availablePartner != null)
                        {
                            deliveryStatus = "Assigned";
                            autoAssignedId = availablePartner.Id;
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Auto-assignment failed during status update");
                    }
                }
                else if (newStatus.Contains("Ready", StringComparison.OrdinalIgnoreCase)) 
                {
                    progress = 3;
                    if (newStatus.Equals("Ready for Delivery", StringComparison.OrdinalIgnoreCase))
                    {
                        // Logic: Ready for Delivery means Prepared by vendor
                    }
                }
                else if (newStatus.Equals("In Transit", StringComparison.OrdinalIgnoreCase) || newStatus.Equals("On the Way", StringComparison.OrdinalIgnoreCase)) 
                {
                    progress = 4;
                    deliveryStatus = "Out for Delivery";
                    newStatus = "On the Way"; // Standardize
                }
                else if (newStatus.Equals("Delivered", StringComparison.OrdinalIgnoreCase) || newStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase)) 
                {
                    progress = 5;
                    deliveryStatus = "Delivered";
                    newStatus = "Delivered"; // Standardize
                }
                
                // Construct the update query
                string updateQ = "UPDATE OrderTable SET Status = @s, TrackingProgress = @p, UpdatedAt = GETDATE()";
                if (deliveryStatus != null) updateQ += ", DeliveryStatus = @ds";
                if (autoAssignedId != null) updateQ += ", DeliveryPersonId = @dp";
                updateQ += " WHERE OrderId = @id";

                using var cmd = new SqlCommand(updateQ, con);
                cmd.Parameters.AddWithValue("@s", newStatus);
                cmd.Parameters.AddWithValue("@p", progress);
                cmd.Parameters.AddWithValue("@id", orderId);
                if (deliveryStatus != null) cmd.Parameters.AddWithValue("@ds", deliveryStatus);
                if (autoAssignedId != null) cmd.Parameters.AddWithValue("@dp", autoAssignedId.Value);
                
                var rows = await cmd.ExecuteNonQueryAsync();

                if (rows > 0)
                {
                    // 🔔 Notify client via SignalR
                    await _hubContext.Clients.All.SendAsync("ReceiveUpdate", $"Order #{orderId} status updated to: {newStatus}");
                }

                return rows > 0;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "UpdateOrderStatusAsync failed");
                return false;
            }
        }

        public async Task<IEnumerable<object>> GetCancelledOrdersAsync()
        {
            var list = new List<object>();
            try
            {
                using var con = GetConn();
                await con.OpenAsync();
                var q = @"
SELECT o.OrderId, o.CustomerName, o.CustomerPhone AS CustomerContact, o.CancelledAt, o.CancelReason, 
       ISNULL(p.Amount, 0) AS RefundedAmount, o.Status
FROM OrderTable o
LEFT JOIN Payment p ON p.OrderId = o.OrderId
WHERE o.Status IN ('Cancelled', 'Canceled', 'canceled')
ORDER BY o.CancelledAt DESC";
                using var cmd = new SqlCommand(q, con);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new
                    {
                        orderId = r.GetInt32(0),
                        customerName = r.IsDBNull(1) ? "" : r.GetString(1),
                        customerContact = r.IsDBNull(2) ? "" : r.GetString(2),
                        cancelledAt = r.IsDBNull(3) ? (DateTime?)null : r.GetDateTime(3),
                        cancelReason = r.IsDBNull(4) ? "" : r.GetString(4),
                        refundedAmount = r.IsDBNull(5) ? 0m : r.GetDecimal(5),
                        status = r.IsDBNull(6) ? "" : r.GetString(6)
                    });
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetCancelledOrdersAsync failed");
            }
            return list;
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
                
                // Optimistic locking: check version
                var qVersion = "SELECT Version FROM OrderTable WHERE OrderId = @id";
                using var cmdV = new SqlCommand(qVersion, con, tx);
                cmdV.Parameters.AddWithValue("@id", orderId);
                var currentVersion = (int)(await cmdV.ExecuteScalarAsync() ?? 0);

                var q = "UPDATE OrderTable SET Status = 'Cancelled', UpdatedAt = GETDATE(), CancelledAt = GETDATE(), Version = Version + 1 WHERE OrderId = @id AND Version = @v";
                using var cmd = new SqlCommand(q, con, tx);
                cmd.Parameters.AddWithValue("@id", orderId);
                cmd.Parameters.AddWithValue("@v", currentVersion);
                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0) { await tx.RollbackAsync(); return false; }

                var payQ = "UPDATE Payment SET RefundStatus = 'Refund Pending', UpdatedAt = GETDATE() WHERE OrderId = @id";
                using var pcmd = new SqlCommand(payQ, con, tx);
                pcmd.Parameters.AddWithValue("@id", orderId);
                await pcmd.ExecuteNonQueryAsync();

                // Remove calorie entries associated with this order
                var calQ = "DELETE FROM DailyCalorieEntry WHERE OrderId = @id";
                using var ccmd = new SqlCommand(calQ, con, tx);
                ccmd.Parameters.AddWithValue("@id", orderId);
                await ccmd.ExecuteNonQueryAsync();

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

        public async Task<bool> VerifyOrderOTPAsync(int orderId, int otp)
        {
            try
            {
                var order = await _db.OrderTables.FindAsync(orderId);
                if (order == null || order.DeliveryOTP != otp || order.IsDelivered == true)
                    return false;

                // SECURITY FIX: OTP can only be verified if the order is out for delivery
                var validStatuses = new[] { "Out for Delivery", "In Transit", "On the Way" };
                if (!validStatuses.Contains(order.DeliveryStatus) && !validStatuses.Contains(order.Status))
                    return false;

                order.IsDelivered = true;
                order.Status = "Delivered";
                order.DeliveryStatus = "Delivered";
                order.OrderStatus = "Delivered";
                order.TrackingProgress = 100;
                order.UpdatedAt = DateTime.Now;

                await _db.SaveChangesAsync();

                // 🔔 Notify client via SignalR
                await _hubContext.Clients.All.SendAsync("ReceiveUpdate", $"Order #{orderId} has been successfully delivered!");

                return true;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "VerifyOrderOTPAsync failed for order {OrderId}", orderId);
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
                
                // Fetching only users who are explicitly registered as Delivery Partners
                // We'll filter for users where Role is 'Delivery'
                var q = "SELECT Id, Name, Phone FROM UserSignup WHERE Status = 'Active' AND Role = 'Delivery'"; 
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
                // Return empty list on error
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

        public async Task<System.Dynamic.ExpandoObject> GetDeliveryDashboardDataAsync(string period = "daily", DateTime? refDate = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var today = DateTime.Today;
                var referenceDate = refDate ?? today;
                var customStartDate = startDate ?? today.AddDays(-7);
                var customEndDate = endDate ?? today;

                // 1. Statistics
                var activePartners = await GetAvailableDeliveryPersonnelAsync();
                int activePartnersCount = activePartners.Count();

                int inTransitCount = _db.OrderTables.Count(o => o.OrderType == "Delivery" && (o.Status == "In Transit" || o.Status == "On the Way" || o.DeliveryStatus == "In Transit" || o.DeliveryStatus == "Out for Delivery"));
                int pendingAssignmentCount = _db.OrderTables.Count(o => o.OrderType == "Delivery" && (o.Status == "Ready for Delivery" || o.Status == "Accepted") && o.DeliveryPersonId == null);
                
                int completedTodayCount = 0;
                if (period == "custom")
                {
                    completedTodayCount = _db.OrderTables.Count(o => o.OrderType == "Delivery" && (o.Status == "Completed" || o.Status == "Delivered") && o.UpdatedAt.HasValue && o.UpdatedAt.Value.Date >= customStartDate.Date && o.UpdatedAt.Value.Date <= customEndDate.Date);
                }
                else
                {
                    completedTodayCount = _db.OrderTables.Count(o => o.OrderType == "Delivery" && (o.Status == "Completed" || o.Status == "Delivered") && o.UpdatedAt >= today);
                }
                
                int completedAllTimeCount = _db.OrderTables.Count(o => o.OrderType == "Delivery" && (o.Status == "Completed" || o.Status == "Delivered"));

                // 2. Partner List with status
                var partners = new List<object>();
                var allPartners = await GetAvailableDeliveryPersonnelAsync();

                foreach (var p in allPartners)
                {
                    dynamic partner = p;
                    int pid = partner.Id;
                    string name = partner.Name;
                    string phone = partner.Phone;

                    // Find if they have an active delivery
                    var activeDelivery = _db.OrderTables
                        .Where(o => o.DeliveryPersonId == pid && 
                                   o.Status != "Completed" && 
                                   o.Status != "Delivered" && 
                                   o.Status != "Cancelled")
                        .OrderByDescending(o => o.CreatedAt)
                        .FirstOrDefault();

                    string status = activeDelivery != null ? "On Delivery" : "Available";
                    string task = activeDelivery != null ? $"Order #{activeDelivery.OrderId} • {activeDelivery.Status}" : "Waiting for assignment";

                    partners.Add(new
                    {
                        Name = name,
                        Phone = phone,
                        Status = status,
                        CurrentTask = task,
                        Rating = 0.0 
                    });
                }

                // 3. Historical Data for Report based on Period
                var historyTrend = new List<object>();
                
                if (period == "weekly")
                {
                    // Last 7 weeks
                    for (int i = 6; i >= 0; i--)
                    {
                        var startOfWeek = referenceDate.AddDays(-(int)referenceDate.DayOfWeek - (i * 7));
                        var endOfWeek = startOfWeek.AddDays(6);
                        var count = _db.OrderTables.Count(o => o.OrderType == "Delivery" && 
                                                             (o.Status == "Completed" || o.Status == "Delivered") && 
                                                             o.UpdatedAt.HasValue && 
                                                             o.UpdatedAt.Value.Date >= startOfWeek.Date && 
                                                             o.UpdatedAt.Value.Date <= endOfWeek.Date);
                        
                        historyTrend.Add(new { Label = "Week " + (7-i), Value = count });
                    }
                }
                else if (period == "monthly")
                {
                    // Last 6 months
                    for (int i = 5; i >= 0; i--)
                    {
                        var monthDate = referenceDate.AddMonths(-i);
                        var count = _db.OrderTables.Count(o => o.OrderType == "Delivery" && 
                                                             (o.Status == "Completed" || o.Status == "Delivered") && 
                                                             o.UpdatedAt.HasValue && 
                                                             o.UpdatedAt.Value.Month == monthDate.Month && 
                                                             o.UpdatedAt.Value.Year == monthDate.Year);
                        
                        historyTrend.Add(new { Label = monthDate.ToString("MMM yyyy"), Value = count });
                    }
                }
                else if (period == "custom")
                {
                    int totalDays = (int)(customEndDate.Date - customStartDate.Date).TotalDays;
                    int maxDays = Math.Min(totalDays, 30); // 30 points max
                    
                    for (int i = maxDays; i >= 0; i--)
                    {
                        var date = customEndDate.AddDays(-i);
                        if (date < customStartDate.Date) continue;
                        
                        var count = _db.OrderTables.Count(o => o.OrderType == "Delivery" && 
                                                             (o.Status == "Completed" || o.Status == "Delivered") && 
                                                             o.UpdatedAt.HasValue && 
                                                             o.UpdatedAt.Value.Date == date.Date);
                        
                        historyTrend.Add(new { Label = date.ToString("MMM dd"), Value = count });
                    }
                }
                else if (period == "yearly")
                {
                    // Last 5 years
                    for (int i = 4; i >= 0; i--)
                    {
                        var yearDate = referenceDate.AddYears(-i);
                        var count = _db.OrderTables.Count(o => o.OrderType == "Delivery" && 
                                                             (o.Status == "Completed" || o.Status == "Delivered") && 
                                                             o.UpdatedAt.HasValue && 
                                                             o.UpdatedAt.Value.Year == yearDate.Year);
                        
                        historyTrend.Add(new { Label = yearDate.Year.ToString(), Value = count });
                    }
                }
                else // Default: Daily (Last 7 days)
                {
                    for (int i = 6; i >= 0; i--)
                    {
                        var date = referenceDate.AddDays(-i);
                        var count = _db.OrderTables.Count(o => o.OrderType == "Delivery" && 
                                                             (o.Status == "Completed" || o.Status == "Delivered") && 
                                                             o.UpdatedAt.HasValue && 
                                                             o.UpdatedAt.Value.Date == date.Date);
                        
                        historyTrend.Add(new { Label = date.ToString("MMM dd"), Value = count });
                    }
                }

                // 4. Recent Completed Deliveries
                var recentDeliveries = _db.OrderTables
                    .Where(o => o.OrderType == "Delivery" && (o.Status == "Completed" || o.Status == "Delivered"))
                    .OrderByDescending(o => o.UpdatedAt)
                    .Take(5)
                    .Select(o => new {
                        o.OrderId,
                        o.CustomerName,
                        o.TotalAmount,
                        o.Status,
                        CompletedAt = o.UpdatedAt
                    })
                    .ToList();

                dynamic result = new ExpandoObject();
                result.ActivePartnersCount = activePartnersCount;
                result.InTransitCount = inTransitCount;
                result.PendingAssignmentCount = pendingAssignmentCount;
                result.CompletedTodayCount = completedTodayCount;
                result.CompletedAllTimeCount = completedAllTimeCount;
                result.Partners = partners;
                result.HistoryTrend = historyTrend;
                result.RecentDeliveries = recentDeliveries;

                return result;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetDeliveryDashboardDataAsync failed");
                dynamic errorResult = new ExpandoObject();
                errorResult.ActivePartnersCount = 0;
                errorResult.InTransitCount = 0;
                errorResult.PendingAssignmentCount = 0;
                errorResult.CompletedTodayCount = 0;
                errorResult.CompletedAllTimeCount = 0;
                errorResult.Partners = new List<object>();
                errorResult.HistoryTrend = new List<object>();
                errorResult.RecentDeliveries = new List<object>();
                
                return errorResult;
            }
        }
    }
}