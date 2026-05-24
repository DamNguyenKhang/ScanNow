using ScanNow.Domain.Entities;

namespace ScanNow.Domain.Abstractions.Persistence
{
    public interface IKitchenRepository
    {
        Task<List<OrderItem>> GetActiveKitchenItemsAsync(Guid branchId, CancellationToken ct = default);
        Task<List<OrderItem>> GetOrderItemsByIdsAsync(List<Guid> itemIds, CancellationToken ct = default);
        Task<List<Order>> GetOrdersByIdsAsync(List<Guid> orderIds, CancellationToken ct = default);
    }
}
