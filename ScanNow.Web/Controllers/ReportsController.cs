using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.Reports.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("api/owner/reports/overview")]
        [Authorize(Roles = nameof(UserRole.OWNER))]
        public async Task<ActionResult<ApiResponse<OwnerReportResponse>>> GetOwnerReport([FromQuery] ReportQuery query)
        {
            return new ApiResponse<OwnerReportResponse>
            {
                Result = await _reportService.GetOwnerReportAsync(query),
                Message = "Get owner report successfully"
            };
        }

        [HttpGet("api/manager/reports/overview")]
        [Authorize(Roles = nameof(UserRole.BRANCH_MANAGER))]
        public async Task<ActionResult<ApiResponse<OwnerReportResponse>>> GetManagerReport([FromQuery] ReportQuery query)
        {
            return new ApiResponse<OwnerReportResponse>
            {
                Result = await _reportService.GetBranchManagerReportAsync(query),
                Message = "Get manager report successfully"
            };
        }

        [HttpGet("api/admin/reports/dashboard")]
        [Authorize(Roles = nameof(UserRole.ADMIN))]
        public async Task<ActionResult<ApiResponse<AdminDashboardReportResponse>>> GetAdminDashboard()
        {
            return new ApiResponse<AdminDashboardReportResponse>
            {
                Result = await _reportService.GetAdminDashboardAsync(),
                Message = "Get admin dashboard successfully"
            };
        }
    }
}
