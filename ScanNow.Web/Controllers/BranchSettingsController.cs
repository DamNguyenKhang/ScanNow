using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.BranchSettings.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Authorize(Roles = $"{nameof(UserRole.OWNER)},{nameof(UserRole.BRANCH_MANAGER)}")]
    public class BranchSettingsController : ControllerBase
    {
        private readonly IBranchSettingsService _settingsService;

        public BranchSettingsController(IBranchSettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        [HttpGet("api/owner/branches/{branchId:guid}/payment-config")]
        [HttpGet("api/manager/branches/{branchId:guid}/payment-config")]
        public async Task<ActionResult<ApiResponse<BranchPaymentConfigResponse>>> GetPaymentConfig(Guid branchId)
        {
            return new ApiResponse<BranchPaymentConfigResponse>
            {
                Result = await _settingsService.GetPaymentConfigAsync(branchId),
                Message = "Get payment config successfully"
            };
        }

        [HttpPut("api/owner/branches/{branchId:guid}/payment-config")]
        [HttpPut("api/manager/branches/{branchId:guid}/payment-config")]
        public async Task<ActionResult<ApiResponse<BranchPaymentConfigResponse>>> UpsertPaymentConfig(Guid branchId, [FromBody] UpsertBranchPaymentConfigRequest request)
        {
            return new ApiResponse<BranchPaymentConfigResponse>
            {
                Result = await _settingsService.UpsertPaymentConfigAsync(branchId, request),
                Message = "Save payment config successfully"
            };
        }

        [HttpGet("api/owner/branches/{branchId:guid}/paper-vouchers")]
        [HttpGet("api/manager/branches/{branchId:guid}/paper-vouchers")]
        public async Task<ActionResult<ApiResponse<List<PaperVoucherResponse>>>> GetPaperVouchers(Guid branchId)
        {
            return new ApiResponse<List<PaperVoucherResponse>>
            {
                Result = await _settingsService.GetPaperVouchersAsync(branchId),
                Message = "Get paper vouchers successfully"
            };
        }

        [HttpPost("api/owner/branches/{branchId:guid}/paper-vouchers")]
        [HttpPost("api/manager/branches/{branchId:guid}/paper-vouchers")]
        public async Task<ActionResult<ApiResponse<PaperVoucherResponse>>> CreatePaperVoucher(Guid branchId, [FromBody] CreatePaperVoucherRequest request)
        {
            return new ApiResponse<PaperVoucherResponse>
            {
                Result = await _settingsService.CreatePaperVoucherAsync(branchId, request),
                Message = "Create paper voucher successfully"
            };
        }

        [HttpPut("api/owner/branches/{branchId:guid}/paper-vouchers/{voucherId:guid}")]
        [HttpPut("api/manager/branches/{branchId:guid}/paper-vouchers/{voucherId:guid}")]
        public async Task<ActionResult<ApiResponse<PaperVoucherResponse>>> UpdatePaperVoucher(Guid branchId, Guid voucherId, [FromBody] UpdatePaperVoucherRequest request)
        {
            return new ApiResponse<PaperVoucherResponse>
            {
                Result = await _settingsService.UpdatePaperVoucherAsync(branchId, voucherId, request),
                Message = "Update paper voucher successfully"
            };
        }
    }
}
