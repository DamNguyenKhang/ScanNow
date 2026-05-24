using ScanNow.Application.Features.Order.DTOs;

namespace ScanNow.Application.Abstractions
{
    public interface IOrderService
    {
        Task<CustomerOrderResponse> PlaceOrderAsync(string sessionCode, PlaceOrderRequest request);
        Task<CustomerOrderResponse> GetPublicOrderDetailAsync(string sessionCode, Guid orderId);
        Task CancelOrderAsync(Guid orderId, Guid branchId);
    }
}
