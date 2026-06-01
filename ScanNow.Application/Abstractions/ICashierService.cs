using ScanNow.Application.Features.Cashier.DTOs;
using ScanNow.Application.Features.Order.DTOs;
using ScanNow.Application.Features.RestaurantManagement.DTOs;

namespace ScanNow.Application.Abstractions
{
    public interface ICashierService
    {
        Task<PagedResult<TableOrderHistoryResponse>> GetBranchOrdersAsync(Guid branchId, CashierOrderQuery query);
        Task<TableOrderHistoryResponse> GetBranchOrderAsync(Guid branchId, Guid orderId);
        Task<CashierPaymentResponse> CheckoutAsync(Guid branchId, Guid orderId, CashierCheckoutRequest request);
        Task<TableOrderHistoryResponse> CancelPendingPaymentAsync(Guid branchId, Guid orderId);
    }
}
