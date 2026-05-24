using ScanNow.Domain.Entities;

namespace ScanNow.Domain.Abstractions.Persistence
{
    public interface IOrderRepository
    {
        Task<QrSession?> GetActiveSessionByCodeAsync(string sessionCode, CancellationToken ct = default);
        Task<MenuItem?> GetMenuItemByIdAsync(Guid menuItemId, CancellationToken ct = default);
        Task<Order?> GetActiveOrderByIdAsync(Guid orderId, CancellationToken ct = default);
        Task<Order?> GetActiveSessionOrderAsync(string sessionCode, Guid orderId, CancellationToken ct = default);
        Task<Order?> GetOrderWithPaymentsAsync(Guid orderId, CancellationToken ct = default);
        Task AddOrderAsync(Order order, CancellationToken ct = default);
    }
}
