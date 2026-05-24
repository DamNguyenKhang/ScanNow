using ScanNow.Application.Features.Order.DTOs;

namespace ScanNow.Application.Abstractions
{
    public interface IOrderService
    {
        Task<OrderResponse> PlaceOrderAsync(string sessionCode, PlaceOrderRequest request);
    }
}
