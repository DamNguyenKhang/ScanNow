using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.Kitchen.DTOs;

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

        [HttpPost("api/kitchen/items/start-cooking")]
        public async Task<ActionResult<ApiResponse<StartCookingResponse>>> StartCooking(
            [FromQuery] Guid branchId,
            [FromBody] StartCookingRequest request)
        {
            return new ApiResponse<StartCookingResponse>
            {
                Result = await _kitchenService.StartCookingItemsAsync(request, branchId),
                Message = "Items started cooking successfully"
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
