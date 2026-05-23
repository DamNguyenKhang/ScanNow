using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.TableQr.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Authorize(Roles = $"{nameof(UserRole.OWNER)},{nameof(UserRole.BRANCH_MANAGER)}")]
    public class OwnerTablesController : ControllerBase
    {
        private readonly ITableQrService _tableQrService;

        public OwnerTablesController(ITableQrService tableQrService)
        {
            _tableQrService = tableQrService;
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
