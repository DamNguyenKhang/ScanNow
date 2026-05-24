using ScanNow.Application.Features.Order.DTOs;

namespace ScanNow.Application.Abstractions
{
    public interface IOrderUpdatePublisher
    {
        Task PublishOrderUpdatedAsync(CustomerOrderResponse order, CancellationToken ct = default);
    }
}
