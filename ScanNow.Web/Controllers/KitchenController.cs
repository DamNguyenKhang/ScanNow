using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.Kitchen.DTOs;
using ScanNow.Application.Features.Waiter.DTOs;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Authorize(Roles = "KITCHEN,BRANCH_MANAGER")]
    public class KitchenController : ControllerBase
    {
        private readonly IKitchenService _kitchenService;

        public KitchenController(IKitchenService kitchenService)
        {
            _kitchenService = kitchenService;
        }

        [HttpGet("api/kitchen/orders/pending-confirmation")]
        public async Task<ActionResult<ApiResponse<List<PendingOrderResponse>>>> GetPendingOrders([FromQuery] Guid branchId)
        {
            return new ApiResponse<List<PendingOrderResponse>>
            {
                Result = await _kitchenService.GetPendingConfirmationOrdersAsync(branchId),
                Message = "Get pending kitchen orders successfully"
            };
        }

        [HttpPost("api/kitchen/orders/{orderId}/confirm")]
        public async Task<ActionResult<ApiResponse<ConfirmOrderResponse>>> ConfirmOrder(Guid orderId, [FromQuery] Guid branchId)
        {
            return new ApiResponse<ConfirmOrderResponse>
            {
                Result = await _kitchenService.ConfirmOrderAsync(orderId, branchId),
                Message = "Order confirmed by kitchen successfully"
            };
        }

        [HttpPost("api/kitchen/items/confirm")]
        public async Task<ActionResult<ApiResponse<ConfirmKitchenItemsResponse>>> ConfirmItems(
            [FromQuery] Guid branchId,
            [FromBody] ConfirmKitchenItemsRequest request)
        {
            return new ApiResponse<ConfirmKitchenItemsResponse>
            {
                Result = await _kitchenService.ConfirmItemsAsync(request, branchId),
                Message = "Items confirmed by kitchen successfully"
            };
        }

        [HttpGet("api/kitchen/items/grouped")]
        public async Task<ActionResult<ApiResponse<List<GroupedKitchenItemDto>>>> GetGroupedItems(
            [FromQuery] Guid branchId,
            [FromQuery] string? status = null)
        {
            return new ApiResponse<List<GroupedKitchenItemDto>>
            {
                Result = await _kitchenService.GetGroupedKitchenItemsAsync(branchId, status),
                Message = "Get grouped kitchen items successfully"
            };
        }

        [HttpPost("api/kitchen/items/mark-ready")]
        public async Task<ActionResult<ApiResponse<MarkReadyResponse>>> MarkReady(
            [FromQuery] Guid branchId,
            [FromBody] MarkReadyRequest request)
        {
            return new ApiResponse<MarkReadyResponse>
            {
                Result = await _kitchenService.MarkItemsReadyAsync(request, branchId),
                Message = "Items marked as ready successfully"
            };
        }
    }
}
