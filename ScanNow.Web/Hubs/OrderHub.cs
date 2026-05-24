using Microsoft.AspNetCore.SignalR;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Features.Order.DTOs;

namespace ScanNow.Web.Hubs
{
    public class OrderHub : Hub
    {
        private readonly IOrderService _orderService;

        public OrderHub(IOrderService orderService)
        {
            _orderService = orderService;
        }

        public async Task<CustomerOrderResponse> JoinOrder(string sessionCode, Guid orderId)
        {
            var order = await _orderService.GetPublicOrderDetailAsync(sessionCode, orderId);
            await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(orderId));
            return order;
        }

        public Task LeaveOrder(Guid orderId)
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(orderId));
        }

        internal static string GetGroupName(Guid orderId) => $"order:{orderId:N}";
    }
}
