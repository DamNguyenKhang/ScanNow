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

        public Task JoinBranch(Guid branchId)
        {
            return Groups.AddToGroupAsync(Context.ConnectionId, GetBranchGroupName(branchId));
        }

        public Task LeaveBranch(Guid branchId)
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, GetBranchGroupName(branchId));
        }

        internal static string GetGroupName(Guid orderId) => $"order:{orderId:N}";
        internal static string GetBranchGroupName(Guid branchId) => $"branch:{branchId:N}";
    }
}
