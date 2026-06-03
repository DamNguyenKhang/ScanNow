using Microsoft.AspNetCore.SignalR;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Features.Order.DTOs;

namespace ScanNow.Web.Hubs
{
    public class SignalROrderUpdatePublisher : IOrderUpdatePublisher
    {
        private readonly IHubContext<OrderHub> _hubContext;

        public SignalROrderUpdatePublisher(IHubContext<OrderHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task PublishOrderUpdatedAsync(CustomerOrderResponse order, CancellationToken ct = default)
        {
            return Task.WhenAll(
                _hubContext.Clients
                    .Group(OrderHub.GetGroupName(order.OrderId))
                    .SendAsync("OrderUpdated", order, ct),
                _hubContext.Clients
                    .Group(OrderHub.GetBranchGroupName(order.BranchId))
                    .SendAsync("BranchOrderUpdated", order, ct));
        }
    }
}
