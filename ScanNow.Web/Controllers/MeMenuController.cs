using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.MenuManagement.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Authorize(Roles = $"{nameof(UserRole.STAFF)},{nameof(UserRole.CASHIER)},{nameof(UserRole.KITCHEN)}")]
    public class MeMenuController : ControllerBase
    {
        private readonly IMenuManagementService _menuManagementService;

        public MeMenuController(IMenuManagementService menuManagementService)
        {
            _menuManagementService = menuManagementService;
        }

        [HttpGet("api/me/branches/{branchId:guid}/menu")]
        public async Task<ActionResult<ApiResponse<PagedResult<MenuCategoryResponse>>>> GetMenu(Guid branchId, [FromQuery] MenuQuery query)
        {
            return new ApiResponse<PagedResult<MenuCategoryResponse>>
            {
                Result = await _menuManagementService.GetMyBranchMenuAsync(branchId, query),
                Message = "Get menu successfully"
            };
        }

        [HttpGet("api/me/menu-items/{id:guid}")]
        public async Task<ActionResult<ApiResponse<MenuItemResponse>>> GetMenuItem(Guid id)
        {
            return new ApiResponse<MenuItemResponse>
            {
                Result = await _menuManagementService.GetMyMenuItemAsync(id),
                Message = "Get menu item successfully"
            };
        }

        [HttpPatch("api/me/menu-items/{id:guid}/toggle-available")]
        public async Task<ActionResult<ApiResponse<MenuItemResponse>>> ToggleAvailable(Guid id)
        {
            return new ApiResponse<MenuItemResponse>
            {
                Result = await _menuManagementService.ToggleMyMenuItemAvailableAsync(id),
                Message = "Toggle available successfully"
            };
        }

        [HttpPatch("api/me/branches/{branchId:guid}/menu-items/bulk-availability")]
        public async Task<ActionResult<ApiResponse<IReadOnlyList<MenuItemResponse>>>> BulkAvailability(Guid branchId, [FromBody] BulkAvailabilityRequest request)
        {
            return new ApiResponse<IReadOnlyList<MenuItemResponse>>
            {
                Result = await _menuManagementService.BulkUpdateMyAvailabilityAsync(branchId, request),
                Message = "Update availability successfully"
            };
        }
    }
}
