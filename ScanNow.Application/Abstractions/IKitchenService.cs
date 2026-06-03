using ScanNow.Application.Features.Kitchen.DTOs;
using ScanNow.Application.Features.Waiter.DTOs;

namespace ScanNow.Application.Abstractions
{
    public interface IKitchenService
    {
        Task<List<PendingOrderResponse>> GetPendingConfirmationOrdersAsync(Guid branchId);
        Task<ConfirmOrderResponse> ConfirmOrderAsync(Guid orderId, Guid branchId);
        Task<ConfirmKitchenItemsResponse> ConfirmItemsAsync(ConfirmKitchenItemsRequest request, Guid branchId);
        Task<List<GroupedKitchenItemDto>> GetGroupedKitchenItemsAsync(Guid branchId, string? status = null);
        Task<MarkReadyResponse> MarkItemsReadyAsync(MarkReadyRequest request, Guid branchId);
    }
}
