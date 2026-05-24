using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.Checkout.DTOs;
using ScanNow.Application.Features.MenuManagement.DTOs;
using ScanNow.Application.Features.Order.DTOs;
using ScanNow.Application.Features.TableQr.DTOs;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    public class PublicTableQrController : ControllerBase
    {
        private readonly ITableQrService _tableQrService;
        private readonly IOrderService _orderService;
        private readonly ICheckoutService _checkoutService;

        public PublicTableQrController(
            ITableQrService tableQrService,
            IOrderService orderService,
            ICheckoutService checkoutService)
        {
            _tableQrService = tableQrService;
            _orderService = orderService;
            _checkoutService = checkoutService;
        }

        [HttpGet("api/public/tables/{qrCodeToken}")]
        public async Task<ActionResult<ApiResponse<PublicTableResponse>>> GetTable(string qrCodeToken)
        {
            return new ApiResponse<PublicTableResponse>
            {
                Result = await _tableQrService.GetPublicTableAsync(qrCodeToken),
                Message = "Get table successfully"
            };
        }

        [HttpPost("api/public/sessions/join")]
        public async Task<ActionResult<ApiResponse<JoinSessionResponse>>> JoinSession([FromBody] JoinSessionRequest request)
        {
            return new ApiResponse<JoinSessionResponse>
            {
                Result = await _tableQrService.JoinSessionAsync(request),
                Message = "Join session successfully"
            };
        }

        [HttpGet("api/public/sessions/{sessionCode}/menu")]
        public async Task<ActionResult<ApiResponse<SessionMenuResponse>>> GetSessionMenu(string sessionCode, [FromQuery] MenuQuery query)
        {
            return new ApiResponse<SessionMenuResponse>
            {
                Result = await _tableQrService.GetSessionMenuAsync(sessionCode, query),
                Message = "Get session menu successfully"
            };
        }

        [HttpPost("api/public/sessions/{sessionCode}/orders")]
        public async Task<ActionResult<ApiResponse<CustomerOrderResponse>>> PlaceOrder(
            string sessionCode,
            [FromBody] PlaceOrderRequest request)
        {
            return new ApiResponse<CustomerOrderResponse>
            {
                Result = await _orderService.PlaceOrderAsync(sessionCode, request),
                Message = "Order placed successfully"
            };
        }

        [HttpPost("api/public/sessions/{sessionCode}/checkout")]
        public async Task<ActionResult<ApiResponse<CheckoutResponse>>> Checkout(
            string sessionCode,
            [FromBody] CreateCheckoutRequest request)
        {
            return new ApiResponse<CheckoutResponse>
            {
                Result = await _checkoutService.CreateCheckoutAsync(sessionCode, request),
                Message = "Checkout created successfully"
            };
        }

        [HttpGet("api/public/sessions/{sessionCode}/payment-status")]
        public async Task<ActionResult<ApiResponse<PaymentStatusResponse>>> GetPaymentStatus(string sessionCode)
        {
            return new ApiResponse<PaymentStatusResponse>
            {
                Result = await _checkoutService.GetPaymentStatusAsync(sessionCode),
                Message = "Payment status retrieved"
            };
        }

        [HttpGet("api/public/sessions/{sessionCode}/orders/{orderId}")]
        public async Task<ActionResult<ApiResponse<CustomerOrderResponse>>> GetOrderDetail(string sessionCode, Guid orderId)
        {
            return new ApiResponse<CustomerOrderResponse>
            {
                Result = await _orderService.GetPublicOrderDetailAsync(sessionCode, orderId),
                Message = "Get order detail successfully"
            };
        }
    }
}
