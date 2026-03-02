using Microsoft.AspNetCore.Mvc;
using NUTRIBITE.Services;
using NutriBite.Filters;
using System.Threading.Tasks;

namespace NUTRIBITE.Controllers
{
    [AdminAuthorize]
    public class AdminOrdersController : Controller
    {
        private readonly IOrderService _orderService;
        public AdminOrdersController(IOrderService orderService) => _orderService = orderService;

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptOrderAndRedirect(int orderId)
        {
            var ok = await _orderService.UpdateOrderStatusAsync(orderId, "Accepted");
            if (!ok)
            {
                TempData["Error"] = "Unable to accept order.";
                return RedirectToAction("OrderManagement", "Admin");
            }
            TempData["Success"] = "Order accepted.";
            return RedirectToAction("OrderManagement", "Admin");
        }
    }
}