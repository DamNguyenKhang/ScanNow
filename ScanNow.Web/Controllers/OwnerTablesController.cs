using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.Order.DTOs;
using ScanNow.Application.Features.TableQr.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Authorize(Roles = $"{nameof(UserRole.OWNER)},{nameof(UserRole.BRANCH_MANAGER)}")]
    public class OwnerTablesController : ControllerBase
    {
        private readonly ITableQrService _tableQrService;
        private readonly IOrderService _orderService;

        public OwnerTablesController(ITableQrService tableQrService, IOrderService orderService)
        {
            _tableQrService = tableQrService;
            _orderService = orderService;
        }

        [HttpGet("api/owner/branches/{branchId:guid}/tables")]
        public async Task<ActionResult<ApiResponse<ScanNow.Application.Features.RestaurantManagement.DTOs.PagedResult<TableResponse>>>> GetTables(Guid branchId, [FromQuery] TableQuery query)
        {
            return new ApiResponse<ScanNow.Application.Features.RestaurantManagement.DTOs.PagedResult<TableResponse>>
            {
                Result = await _tableQrService.GetManageTablesAsync(branchId, query),
                Message = "Get tables successfully"
            };
        }

        [HttpGet("api/owner/branches/{branchId:guid}/tables/{id:guid}")]
        public async Task<ActionResult<ApiResponse<TableResponse>>> GetTable(Guid branchId, Guid id)
        {
            return new ApiResponse<TableResponse>
            {
                Result = await _tableQrService.GetManageTableAsync(branchId, id),
                Message = "Get table successfully"
            };
        }

        [HttpGet("api/owner/branches/{branchId:guid}/tables/{id:guid}/orders")]
        public async Task<ActionResult<ApiResponse<List<TableOrderHistoryResponse>>>> GetTableOrderHistory(Guid branchId, Guid id)
        {
            await _tableQrService.GetManageTableAsync(branchId, id);

            return new ApiResponse<List<TableOrderHistoryResponse>>
            {
                Result = await _orderService.GetBranchTableOrderHistoryAsync(branchId, id),
                Message = "Get table order history successfully"
            };
        }

        [HttpGet("api/owner/branches/{branchId:guid}/orders")]
        public async Task<ActionResult<ApiResponse<OrderInvoiceListResponse>>> GetBranchOrders(Guid branchId, [FromQuery] OrderInvoiceQuery query)
        {
            await _tableQrService.GetManageTablesAsync(branchId, new TableQuery { PageNumber = 1, PageSize = 1 });

            return new ApiResponse<OrderInvoiceListResponse>
            {
                Result = await _orderService.GetBranchOrdersAsync(branchId, query),
                Message = "Get branch orders successfully"
            };
        }

        [HttpPost("api/owner/branches/{branchId:guid}/tables")]
        public async Task<ActionResult<ApiResponse<TableResponse>>> CreateTable(Guid branchId, [FromBody] CreateTableRequest request)
        {
            return new ApiResponse<TableResponse>
            {
                Result = await _tableQrService.CreateTableAsync(branchId, request),
                Message = "Create table successfully"
            };
        }

        [HttpPut("api/owner/tables/{id:guid}")]
        public async Task<ActionResult<ApiResponse<TableResponse>>> UpdateTable(Guid id, [FromBody] UpdateTableRequest request)
        {
            return new ApiResponse<TableResponse>
            {
                Result = await _tableQrService.UpdateTableAsync(id, request),
                Message = "Update table successfully"
            };
        }

        [HttpPatch("api/owner/tables/{id:guid}/status")]
        public async Task<ActionResult<ApiResponse<TableResponse>>> UpdateStatus(Guid id, [FromBody] UpdateTableStatusRequest request)
        {
            return new ApiResponse<TableResponse>
            {
                Result = await _tableQrService.UpdateTableStatusAsync(id, request),
                Message = "Update table status successfully"
            };
        }

        [HttpPatch("api/owner/tables/{id:guid}/activate")]
        public async Task<ActionResult<ApiResponse<TableResponse>>> ActivateTable(Guid id)
        {
            return new ApiResponse<TableResponse>
            {
                Result = await _tableQrService.ActivateTableAsync(id),
                Message = "Activate table successfully"
            };
        }

        [HttpPatch("api/owner/tables/{id:guid}/deactivate")]
        public async Task<ActionResult<ApiResponse<TableResponse>>> DeactivateTable(Guid id)
        {
            return new ApiResponse<TableResponse>
            {
                Result = await _tableQrService.DeactivateTableAsync(id),
                Message = "Deactivate table successfully"
            };
        }

        [HttpPost("api/owner/tables/{id:guid}/regenerate-qr")]
        public async Task<ActionResult<ApiResponse<TableResponse>>> RegenerateQr(Guid id)
        {
            return new ApiResponse<TableResponse>
            {
                Result = await _tableQrService.RegenerateQrAsync(id),
                Message = "Regenerate QR successfully"
            };
        }

        [HttpGet("api/owner/tables/{id:guid}/qr-image")]
        public async Task<IActionResult> GetQrImage(Guid id)
        {
            return File(await _tableQrService.GetQrImageAsync(id), "image/png", $"table-{id}-qr.png");
        }
    }
}
