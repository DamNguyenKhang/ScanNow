using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.TableQr.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Route("api/admin/branches/{branchId:guid}")]
    [Authorize(Roles = nameof(UserRole.ADMIN))]
    public class AdminTablesController : ControllerBase
    {
        private readonly ITableQrService _tableQrService;

        public AdminTablesController(ITableQrService tableQrService)
        {
            _tableQrService = tableQrService;
        }

        [HttpGet("tables")]
        public async Task<ActionResult<ApiResponse<ScanNow.Application.Features.RestaurantManagement.DTOs.PagedResult<TableResponse>>>> GetTables(Guid branchId, [FromQuery] TableQuery query)
        {
            return new ApiResponse<ScanNow.Application.Features.RestaurantManagement.DTOs.PagedResult<TableResponse>>
            {
                Result = await _tableQrService.GetAdminTablesAsync(branchId, query),
                Message = "Get tables successfully"
            };
        }

        [HttpGet("tables/{id:guid}")]
        public async Task<ActionResult<ApiResponse<TableResponse>>> GetTable(Guid branchId, Guid id)
        {
            return new ApiResponse<TableResponse>
            {
                Result = await _tableQrService.GetAdminTableAsync(branchId, id),
                Message = "Get table successfully"
            };
        }

        [HttpGet("sessions")]
        public async Task<ActionResult<ApiResponse<IReadOnlyList<QrSessionResponse>>>> GetSessions(Guid branchId)
        {
            return new ApiResponse<IReadOnlyList<QrSessionResponse>>
            {
                Result = await _tableQrService.GetAdminSessionsAsync(branchId),
                Message = "Get sessions successfully"
            };
        }
    }
}
