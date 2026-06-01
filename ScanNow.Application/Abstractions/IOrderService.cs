using ScanNow.Application.Features.Order.DTOs;

namespace ScanNow.Application.Abstractions
{
    public interface IOrderService
    {
        Task<CustomerOrderResponse> PlaceOrderAsync(string sessionCode, PlaceOrderRequest request);
        Task<CustomerOrderResponse> GetPublicOrderDetailAsync(string sessionCode, Guid orderId);
        Task<OrderInvoiceListResponse> GetBranchOrdersAsync(Guid branchId, OrderInvoiceQuery query);
        Task<List<TableOrderHistoryResponse>> GetBranchTableOrderHistoryAsync(Guid branchId, Guid tableId);
        Task<List<TableOrderHistoryResponse>> GetActiveBranchTableOrdersAsync(Guid branchId, Guid tableId);
        Task CancelOrderAsync(Guid orderId, Guid branchId);
    }
}
