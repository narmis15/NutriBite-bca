using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NUTRIBITE.Services
{
    public interface IOrderService
    {
        Task<IEnumerable<object>> GetActiveOrdersAsync();
        Task<object?> GetOrderDetailsAsync(int orderId);
        Task<bool> UpdateOrderStatusAsync(int orderId, string newStatus);
        Task<bool> ToggleFlagAsync(int orderId);
        Task<IEnumerable<object>> GetFlaggedOrdersAsync();
        Task<IEnumerable<object>> GetPickupSlotsAsync(DateTime? date);
        Task<IEnumerable<object>> GetCancelledOrdersAsync();
        Task<int> GetFlaggedCountAsync();
        Task<int> GetCancelledCountAsync();
        Task<bool> CancelOrderAsync(int orderId);
        Task<bool> TriggerRefundAsync(int orderId);
        Task<bool> VerifyFlagAsync(int orderId);
        Task<bool> UpdateRefundStatusAsync(int orderId, string status);
        Task<bool> AddAdminNoteAsync(int orderId, string note);
        Task<bool> BlockCustomerAsync(string customerPhone);
        Task<bool> UpdateSlotStatusAsync(int slotId, bool isDisabled);
        Task<(bool ok,int currentBookings)> UpdateSlotCapacityAsync(int slotId, int capacity);
        Task<(bool ok,string message)> ToggleSlotBlockAsync(int slotId, DateTime date);

        // Delivery methods
        Task<bool> AssignDeliveryPersonAsync(int orderId, int deliveryPersonId);
        Task<bool> UpdateDeliveryStatusAsync(int orderId, string status);
        Task<IEnumerable<object>> GetAvailableDeliveryPersonnelAsync();
        Task<IEnumerable<object>> GetDeliveriesForPersonAsync(int deliveryPersonId);
    }
}