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
        Task<Order?> GetOrderWithDetailsAsync(Guid orderId, CancellationToken ct = default);
        Task<List<Order>> GetOrdersByBranchAsync(Guid branchId, CancellationToken ct = default);
        Task<List<Order>> GetOrdersByBranchTableAsync(Guid branchId, Guid tableId, CancellationToken ct = default);
        Task<List<Order>> GetActiveSessionOrdersByBranchTableAsync(Guid branchId, Guid tableId, CancellationToken ct = default);
        Task AddOrderAsync(Order order, CancellationToken ct = default);
        Task AddOrderItemsAsync(IEnumerable<OrderItem> items, CancellationToken ct = default);
        Task AddPaymentAsync(Payment payment, CancellationToken ct = default);
        Task<int> MarkPaymentSucceededAsync(Guid paymentId, string? transactionId, DateTime paidAt, CancellationToken ct = default);
        Task<int> MarkOrderCompletedAsync(Guid orderId, DateTime completedAt, CancellationToken ct = default);
        Task<int> TouchOrderAsync(Guid orderId, DateTime updatedAt, CancellationToken ct = default);
        Task<int> MarkPendingPaymentsFailedAsync(Guid orderId, DateTime updatedAt, CancellationToken ct = default);
    }
}
