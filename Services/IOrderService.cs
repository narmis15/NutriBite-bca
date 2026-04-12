using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUTRIBITE.ViewModels;

namespace NUTRIBITE.Services
{
    public interface IOrderService
    {
        Task<IEnumerable<object>> GetActiveOrdersAsync();
        Task<OrderDetailsViewModel> GetOrderDetailsAsync(int orderId);
        Task<bool> UpdateOrderStatusAsync(int orderId, string newStatus);
        Task<IEnumerable<object>> GetCancelledOrdersAsync();
        Task<int> GetCancelledCountAsync();
        Task<bool> CancelOrderAsync(int orderId);
        Task<bool> TriggerRefundAsync(int orderId);
        Task<bool> UpdateRefundStatusAsync(int orderId, string status);
        Task<bool> AddAdminNoteAsync(int orderId, string note);
        Task<bool> BlockCustomerAsync(string customerPhone);

        // Delivery methods
        Task<bool> AssignDeliveryPersonAsync(int orderId, int deliveryPersonId);
        Task<bool> UpdateDeliveryStatusAsync(int orderId, string status);
        Task<bool> VerifyOrderOTPAsync(int orderId, int otp);
        Task<IEnumerable<object>> GetAvailableDeliveryPersonnelAsync();
        Task<IEnumerable<object>> GetDeliveriesForPersonAsync(int deliveryPersonId);
        Task<object> GetDeliveryDashboardDataAsync();

        System.Linq.IQueryable<NUTRIBITE.Models.OrderTable> GetOrderQueryable();
    }
}