using ScanNow.Domain.Entities;

namespace ScanNow.Domain.Abstractions.Persistence
{
    public interface IKitchenRepository
    {
        Task<List<Order>> GetPendingConfirmationOrdersAsync(Guid branchId, CancellationToken ct = default);
        Task<Order?> GetOrderWithItemsAsync(Guid orderId, CancellationToken ct = default);
        Task<List<OrderItem>> GetActiveKitchenItemsAsync(Guid branchId, CancellationToken ct = default);
        Task<List<OrderItem>> GetOrderItemsByIdsAsync(List<Guid> itemIds, CancellationToken ct = default);
        Task<List<Order>> GetOrdersByIdsAsync(List<Guid> orderIds, CancellationToken ct = default);
    }
}
