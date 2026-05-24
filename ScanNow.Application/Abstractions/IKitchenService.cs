using ScanNow.Application.Features.Kitchen.DTOs;

namespace ScanNow.Application.Abstractions
{
    public interface IKitchenService
    {
        Task<List<GroupedKitchenItemDto>> GetGroupedKitchenItemsAsync(Guid branchId, string? status = null);
        Task<StartCookingResponse> StartCookingItemsAsync(StartCookingRequest request, Guid branchId);
        Task<MarkReadyResponse> MarkItemsReadyAsync(MarkReadyRequest request, Guid branchId);
    }
}
