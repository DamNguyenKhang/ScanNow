using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.MenuManagement.DTOs;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Route("api/public/branches/{branchId:guid}")]
    public class PublicMenuController : ControllerBase
    {
        private readonly IMenuManagementService _menuManagementService;

        public PublicMenuController(IMenuManagementService menuManagementService)
        {
            _menuManagementService = menuManagementService;
        }

        [HttpGet("menu")]
        public async Task<ActionResult<ApiResponse<PagedResult<MenuCategoryResponse>>>> GetMenu(Guid branchId, [FromQuery] MenuQuery query)
        {
            return new ApiResponse<PagedResult<MenuCategoryResponse>>
            {
                Result = await _menuManagementService.GetPublicBranchMenuAsync(branchId, query),
                Message = "Get menu successfully"
            };
        }

        [HttpGet("categories")]
        public async Task<ActionResult<ApiResponse<IReadOnlyList<CategoryResponse>>>> GetCategories(Guid branchId)
        {
            return new ApiResponse<IReadOnlyList<CategoryResponse>>
            {
                Result = await _menuManagementService.GetPublicBranchCategoriesAsync(branchId),
                Message = "Get categories successfully"
            };
        }

        [HttpGet("menu-items/{id:guid}")]
        public async Task<ActionResult<ApiResponse<MenuItemResponse>>> GetMenuItem(Guid branchId, Guid id)
        {
            return new ApiResponse<MenuItemResponse>
            {
                Result = await _menuManagementService.GetPublicMenuItemAsync(branchId, id),
                Message = "Get menu item successfully"
            };
        }
    }
}
