using ScanNow.Application.Features.Waiter.DTOs;

namespace ScanNow.Application.Abstractions
{
    public interface IWaiterService
    {
        Task<List<PendingOrderResponse>> GetPendingConfirmationOrdersAsync(Guid branchId);
        Task<ConfirmOrderResponse> ConfirmOrderAsync(Guid orderId, Guid branchId);
        Task<List<ReadyToServeTableGroup>> GetReadyToServeItemsAsync(Guid branchId);
        Task<MarkItemsServedResponse> MarkItemsServedAsync(MarkItemsServedRequest request, Guid branchId);
    }
}
