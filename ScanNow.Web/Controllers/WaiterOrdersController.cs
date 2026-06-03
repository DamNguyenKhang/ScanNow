using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.Order.DTOs;
using ScanNow.Application.Features.Waiter.DTOs;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Authorize(Roles = "STAFF,CASHIER,BRANCH_MANAGER")]
    public class WaiterOrdersController : ControllerBase
    {
        private readonly IWaiterService _waiterService;
        private readonly ICurrentUserService _currentUser;
        private readonly IUserManagementService _userManagementService;

        public WaiterOrdersController(
            IWaiterService waiterService,
            ICurrentUserService currentUser,
            IUserManagementService userManagementService)
        {
            _waiterService = waiterService;
            _currentUser = currentUser;
            _userManagementService = userManagementService;
        }

        [HttpGet("api/waiter/orders/pending-confirmation")]
        public async Task<ActionResult<ApiResponse<List<PendingOrderResponse>>>> GetPendingOrders([FromQuery] Guid branchId)
        {
            return new ApiResponse<List<PendingOrderResponse>>
            {
                Result = await _waiterService.GetPendingConfirmationOrdersAsync(branchId),
                Message = "Get pending orders successfully"
            };
        }

        [HttpPost("api/waiter/orders/{orderId}/confirm")]
        public async Task<ActionResult<ApiResponse<ConfirmOrderResponse>>> ConfirmOrder(Guid orderId, [FromQuery] Guid branchId)
        {
            return new ApiResponse<ConfirmOrderResponse>
            {
                Result = await _waiterService.ConfirmOrderAsync(orderId, branchId),
                Message = "Order confirmed successfully"
            };
        }

        [HttpGet("api/waiter/items/ready-to-serve")]
        public async Task<ActionResult<ApiResponse<List<ReadyToServeTableGroup>>>> GetReadyToServeItems([FromQuery] Guid branchId)
        {
            return new ApiResponse<List<ReadyToServeTableGroup>>
            {
                Result = await _waiterService.GetReadyToServeItemsAsync(branchId),
                Message = "Get ready-to-serve items successfully"
            };
        }

        [HttpPost("api/waiter/items/mark-served")]
        public async Task<ActionResult<ApiResponse<MarkItemsServedResponse>>> MarkItemsServed(
            [FromQuery] Guid branchId,
            [FromBody] MarkItemsServedRequest request)
        {
            return new ApiResponse<MarkItemsServedResponse>
            {
                Result = await _waiterService.MarkItemsServedAsync(request, branchId),
                Message = "Items marked as served successfully"
            };
        }

        [HttpPost("api/waiter/orders")]
        public async Task<ActionResult<ApiResponse<CustomerOrderResponse>>> CreateOrder(
            [FromQuery] Guid branchId,
            [FromBody] CreateWaiterOrderRequest request)
        {
            return new ApiResponse<CustomerOrderResponse>
            {
                Result = await _waiterService.CreateWaiterOrderAsync(branchId, request),
                Message = "Waiter order created successfully"
            };
        }
    }
}
