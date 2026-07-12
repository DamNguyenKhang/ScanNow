using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.Cashier.DTOs;
using ScanNow.Application.Features.Checkout.DTOs;
using ScanNow.Application.Features.Order.DTOs;
using ScanNow.Application.Features.RestaurantManagement.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Authorize(Roles = $"{nameof(UserRole.CASHIER)},{nameof(UserRole.STAFF)},{nameof(UserRole.BRANCH_MANAGER)},{nameof(UserRole.OWNER)}")]
    public class CashierController : ControllerBase
    {
        private readonly ICashierService _cashierService;

        public CashierController(ICashierService cashierService)
        {
            _cashierService = cashierService;
        }

        [HttpGet("api/cashier/branches/{branchId:guid}/orders")]
        public async Task<ActionResult<ApiResponse<PagedResult<TableOrderHistoryResponse>>>> GetOrders(Guid branchId, [FromQuery] CashierOrderQuery query)
        {
            return new ApiResponse<PagedResult<TableOrderHistoryResponse>>
            {
                Result = await _cashierService.GetBranchOrdersAsync(branchId, query),
                Message = "Get cashier orders successfully"
            };
        }

        [HttpGet("api/cashier/branches/{branchId:guid}/orders/{orderId:guid}")]
        public async Task<ActionResult<ApiResponse<TableOrderHistoryResponse>>> GetOrder(Guid branchId, Guid orderId)
        {
            return new ApiResponse<TableOrderHistoryResponse>
            {
                Result = await _cashierService.GetBranchOrderAsync(branchId, orderId),
                Message = "Get cashier order successfully"
            };
        }

        [HttpGet("api/cashier/branches/{branchId:guid}/orders/{orderId:guid}/bill")]
        public async Task<ActionResult<ApiResponse<CashierBillResponse>>> GetBill(Guid branchId, Guid orderId)
        {
            return new ApiResponse<CashierBillResponse>
            {
                Result = await _cashierService.GetBillAsync(branchId, orderId),
                Message = "Get cashier bill successfully"
            };
        }

        [HttpPost("api/cashier/branches/{branchId:guid}/orders/{orderId:guid}/checkout")]
        [Authorize(Roles = $"{nameof(UserRole.CASHIER)},{nameof(UserRole.BRANCH_MANAGER)},{nameof(UserRole.OWNER)}")]
        public async Task<ActionResult<ApiResponse<CashierPaymentResponse>>> Checkout(Guid branchId, Guid orderId, [FromBody] CashierCheckoutRequest request)
        {
            return new ApiResponse<CashierPaymentResponse>
            {
                Result = await _cashierService.CheckoutAsync(branchId, orderId, request),
                Message = "Cashier checkout created successfully"
            };
        }

        [HttpGet("api/cashier/branches/{branchId:guid}/orders/{orderId:guid}/bill/payment-status")]
        [Authorize(Roles = $"{nameof(UserRole.CASHIER)},{nameof(UserRole.BRANCH_MANAGER)},{nameof(UserRole.OWNER)}")]
        public async Task<ActionResult<ApiResponse<PaymentStatusResponse>>> GetBillPaymentStatus(Guid branchId, Guid orderId)
        {
            return new ApiResponse<PaymentStatusResponse>
            {
                Result = await _cashierService.GetBillPaymentStatusAsync(branchId, orderId),
                Message = "Cashier bill payment status retrieved"
            };
        }

        [HttpPost("api/cashier/branches/{branchId:guid}/orders/{orderId:guid}/bill/payment-cancel")]
        [Authorize(Roles = $"{nameof(UserRole.CASHIER)},{nameof(UserRole.BRANCH_MANAGER)},{nameof(UserRole.OWNER)}")]
        public async Task<ActionResult<ApiResponse<CashierBillResponse>>> CancelBillPendingPayment(Guid branchId, Guid orderId)
        {
            return new ApiResponse<CashierBillResponse>
            {
                Result = await _cashierService.CancelBillPendingPaymentAsync(branchId, orderId),
                Message = "Cashier bill pending payment cancelled successfully"
            };
        }

        [HttpPost("api/cashier/branches/{branchId:guid}/orders/{orderId:guid}/payment-cancel")]
        [Authorize(Roles = $"{nameof(UserRole.CASHIER)},{nameof(UserRole.BRANCH_MANAGER)},{nameof(UserRole.OWNER)}")]
        public async Task<ActionResult<ApiResponse<TableOrderHistoryResponse>>> CancelPendingPayment(Guid branchId, Guid orderId)
        {
            return new ApiResponse<TableOrderHistoryResponse>
            {
                Result = await _cashierService.CancelPendingPaymentAsync(branchId, orderId),
                Message = "Cashier pending payment cancelled successfully"
            };
        }
    }
}
