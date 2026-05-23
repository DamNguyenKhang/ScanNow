using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.TableQr.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Authorize(Roles = $"{nameof(UserRole.STAFF)},{nameof(UserRole.KITCHEN)}")]
    public class MeTablesController : ControllerBase
    {
        private readonly ITableQrService _tableQrService;

        public MeTablesController(ITableQrService tableQrService)
        {
            _tableQrService = tableQrService;
        }

        [HttpPost("api/me/branches/{branchId:guid}/tables/{tableId:guid}/open")]
        public async Task<ActionResult<ApiResponse<QrSessionResponse>>> OpenTable(Guid branchId, Guid tableId)
        {
            return new ApiResponse<QrSessionResponse>
            {
                Result = await _tableQrService.OpenTableAsync(branchId, tableId),
                Message = "Open table successfully"
            };
        }

        [HttpPatch("api/me/sessions/{sessionId:guid}/close")]
        public async Task<ActionResult<ApiResponse<QrSessionResponse>>> CloseSession(Guid sessionId)
        {
            return new ApiResponse<QrSessionResponse>
            {
                Result = await _tableQrService.CloseSessionAsync(sessionId),
                Message = "Close session successfully"
            };
        }

        [HttpGet("api/me/branches/{branchId:guid}/tables")]
        public async Task<ActionResult<ApiResponse<ScanNow.Application.Features.RestaurantManagement.DTOs.PagedResult<TableResponse>>>> GetTables(Guid branchId, [FromQuery] TableQuery query)
        {
            return new ApiResponse<ScanNow.Application.Features.RestaurantManagement.DTOs.PagedResult<TableResponse>>
            {
                Result = await _tableQrService.GetMyTablesAsync(branchId, query),
                Message = "Get tables successfully"
            };
        }

        [HttpGet("api/me/tables/{id:guid}")]
        public async Task<ActionResult<ApiResponse<TableResponse>>> GetTable(Guid id)
        {
            return new ApiResponse<TableResponse>
            {
                Result = await _tableQrService.GetMyTableAsync(id),
                Message = "Get table successfully"
            };
        }
    }
}
