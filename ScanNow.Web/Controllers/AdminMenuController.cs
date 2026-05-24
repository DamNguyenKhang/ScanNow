using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.MenuManagement.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Authorize(Roles = nameof(UserRole.ADMIN))]
    public class AdminMenuController : ControllerBase
    {
        private readonly IMenuManagementService _menuManagementService;

        public AdminMenuController(IMenuManagementService menuManagementService)
        {
            _menuManagementService = menuManagementService;
        }

        [HttpGet("api/admin/branches/{branchId:guid}/categories")]
        public async Task<ActionResult<ApiResponse<PagedResult<CategoryResponse>>>> GetCategories(Guid branchId, [FromQuery] CategoryQuery query)
        {
            return new ApiResponse<PagedResult<CategoryResponse>>
            {
                Result = await _menuManagementService.GetAdminCategoriesAsync(branchId, query),
                Message = "Get categories successfully"
            };
        }

        [HttpGet("api/admin/branches/{branchId:guid}/categories/{id:guid}")]
        public async Task<ActionResult<ApiResponse<CategoryResponse>>> GetCategory(Guid branchId, Guid id)
        {
            return new ApiResponse<CategoryResponse>
            {
                Result = await _menuManagementService.GetAdminCategoryAsync(branchId, id),
                Message = "Get category successfully"
            };
        }

        [HttpGet("api/admin/branches/{branchId:guid}/menu-items")]
        public async Task<ActionResult<ApiResponse<PagedResult<MenuItemResponse>>>> GetMenuItems(Guid branchId, [FromQuery] MenuQuery query)
        {
            return new ApiResponse<PagedResult<MenuItemResponse>>
            {
                Result = await _menuManagementService.GetAdminMenuItemsAsync(branchId, query),
                Message = "Get menu items successfully"
            };
        }

        [HttpGet("api/admin/menu-items/{id:guid}")]
        public async Task<ActionResult<ApiResponse<MenuItemResponse>>> GetMenuItem(Guid id)
        {
            return new ApiResponse<MenuItemResponse>
            {
                Result = await _menuManagementService.GetAdminMenuItemAsync(id),
                Message = "Get menu item successfully"
            };
        }

        [HttpGet("api/admin/menu-items/{id:guid}/price-history")]
        public async Task<ActionResult<ApiResponse<IReadOnlyList<PriceHistoryResponse>>>> GetPriceHistory(Guid id)
        {
            return new ApiResponse<IReadOnlyList<PriceHistoryResponse>>
            {
                Result = await _menuManagementService.GetAdminPriceHistoryAsync(id),
                Message = "Get price history successfully"
            };
        }
    }
}
