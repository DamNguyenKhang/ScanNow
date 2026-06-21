using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.MenuManagement.DTOs;
using ScanNow.Domain.Abstractions.External;
using ScanNow.Domain.Enums;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Authorize(Roles = $"{nameof(UserRole.OWNER)},{nameof(UserRole.BRANCH_MANAGER)}")]
    public class OwnerMenuController : ControllerBase
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IMenuManagementService _menuManagementService;

        public OwnerMenuController(IMenuManagementService menuManagementService, IFileStorageService fileStorageService)
        {
            _menuManagementService = menuManagementService;
            _fileStorageService = fileStorageService;
        }

        [HttpGet("api/owner/branches/{branchId:guid}/categories")]
        public async Task<ActionResult<ApiResponse<PagedResult<CategoryResponse>>>> GetCategories(Guid branchId, [FromQuery] CategoryQuery query)
        {
            return new ApiResponse<PagedResult<CategoryResponse>> { Result = await _menuManagementService.GetManageCategoriesAsync(branchId, query), Message = "Get categories successfully" };
        }

        [HttpGet("api/owner/branches/{branchId:guid}/categories/{id:guid}")]
        public async Task<ActionResult<ApiResponse<CategoryResponse>>> GetCategory(Guid branchId, Guid id)
        {
            return new ApiResponse<CategoryResponse> { Result = await _menuManagementService.GetManageCategoryAsync(branchId, id), Message = "Get category successfully" };
        }

        [HttpPost("api/owner/branches/{branchId:guid}/categories")]
        public async Task<ActionResult<ApiResponse<CategoryResponse>>> CreateCategory(Guid branchId, [FromBody] CreateCategoryRequest request)
        {
            return new ApiResponse<CategoryResponse> { Result = await _menuManagementService.CreateCategoryAsync(branchId, request), Message = "Create category successfully" };
        }

        [HttpPut("api/owner/branches/{branchId:guid}/categories/{id:guid}")]
        public async Task<ActionResult<ApiResponse<CategoryResponse>>> UpdateCategory(Guid branchId, Guid id, [FromBody] UpdateCategoryRequest request)
        {
            return new ApiResponse<CategoryResponse> { Result = await _menuManagementService.UpdateCategoryAsync(branchId, id, request), Message = "Update category successfully" };
        }

        [HttpPatch("api/owner/branches/{branchId:guid}/categories/reorder")]
        public async Task<ActionResult<ApiResponse<IReadOnlyList<CategoryResponse>>>> ReorderCategories(Guid branchId, [FromBody] ReorderCategoryRequest request)
        {
            return new ApiResponse<IReadOnlyList<CategoryResponse>> { Result = await _menuManagementService.ReorderCategoriesAsync(branchId, request), Message = "Reorder categories successfully" };
        }

        [HttpPatch("api/owner/branches/{branchId:guid}/categories/{id:guid}/active")]
        public async Task<ActionResult<ApiResponse<CategoryResponse>>> ActiveCategory(Guid branchId, Guid id)
        {
            return new ApiResponse<CategoryResponse> { Result = await _menuManagementService.SetCategoryActiveAsync(branchId, id, true), Message = "Active category successfully" };
        }

        [HttpPatch("api/owner/branches/{branchId:guid}/categories/{id:guid}/inactive")]
        public async Task<ActionResult<ApiResponse<CategoryResponse>>> InactiveCategory(Guid branchId, Guid id)
        {
            return new ApiResponse<CategoryResponse> { Result = await _menuManagementService.SetCategoryActiveAsync(branchId, id, false), Message = "Inactive category successfully" };
        }

        [HttpGet("api/owner/branches/{branchId:guid}/menu-items")]
        public async Task<ActionResult<ApiResponse<PagedResult<MenuItemResponse>>>> GetMenuItems(Guid branchId, [FromQuery] MenuQuery query)
        {
            return new ApiResponse<PagedResult<MenuItemResponse>> { Result = await _menuManagementService.GetManageMenuItemsAsync(branchId, query), Message = "Get menu items successfully" };
        }

        [HttpGet("api/owner/menu-items/{id:guid}")]
        public async Task<ActionResult<ApiResponse<MenuItemResponse>>> GetMenuItem(Guid id)
        {
            return new ApiResponse<MenuItemResponse> { Result = await _menuManagementService.GetManageMenuItemAsync(id), Message = "Get menu item successfully" };
        }

        [HttpPost("api/owner/menu-items/images")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<ActionResult<ApiResponse<IReadOnlyList<string>>>> UploadMenuItemImages([FromForm] List<IFormFile> files)
        {
            return new ApiResponse<IReadOnlyList<string>>
            {
                Result = await _fileStorageService.UploadImageAsync(files),
                Message = "Upload menu item images successfully"
            };
        }

        [HttpPost("api/owner/branches/{branchId:guid}/categories/{categoryId:guid}/menu-items")]
        public async Task<ActionResult<ApiResponse<MenuItemResponse>>> CreateMenuItem(Guid branchId, Guid categoryId, [FromBody] CreateMenuItemRequest request)
        {
            return new ApiResponse<MenuItemResponse> { Result = await _menuManagementService.CreateMenuItemAsync(branchId, categoryId, request), Message = "Create menu item successfully" };
        }

        [HttpPut("api/owner/menu-items/{id:guid}")]
        public async Task<ActionResult<ApiResponse<MenuItemResponse>>> UpdateMenuItem(Guid id, [FromBody] UpdateMenuItemRequest request)
        {
            return new ApiResponse<MenuItemResponse> { Result = await _menuManagementService.UpdateMenuItemAsync(id, request), Message = "Update menu item successfully" };
        }

        [HttpPatch("api/owner/menu-items/{id:guid}/active")]
        public async Task<ActionResult<ApiResponse<MenuItemResponse>>> ActiveMenuItem(Guid id)
        {
            return new ApiResponse<MenuItemResponse> { Result = await _menuManagementService.SetMenuItemActiveAsync(id, true), Message = "Active menu item successfully" };
        }

        [HttpPatch("api/owner/menu-items/{id:guid}/inactive")]
        public async Task<ActionResult<ApiResponse<MenuItemResponse>>> InactiveMenuItem(Guid id)
        {
            return new ApiResponse<MenuItemResponse> { Result = await _menuManagementService.SetMenuItemActiveAsync(id, false), Message = "Inactive menu item successfully" };
        }

        [HttpPatch("api/owner/branches/{branchId:guid}/menu-items/reorder")]
        public async Task<ActionResult<ApiResponse<IReadOnlyList<MenuItemResponse>>>> ReorderMenuItems(Guid branchId, [FromBody] ReorderMenuItemRequest request)
        {
            return new ApiResponse<IReadOnlyList<MenuItemResponse>> { Result = await _menuManagementService.ReorderMenuItemsAsync(branchId, request), Message = "Reorder menu items successfully" };
        }

        [HttpPatch("api/owner/menu-items/{id:guid}/toggle-available")]
        public async Task<ActionResult<ApiResponse<MenuItemResponse>>> ToggleAvailable(Guid id)
        {
            return new ApiResponse<MenuItemResponse> { Result = await _menuManagementService.ToggleMenuItemAvailableAsync(id), Message = "Toggle available successfully" };
        }

        [HttpPatch("api/owner/branches/{branchId:guid}/menu-items/bulk-availability")]
        public async Task<ActionResult<ApiResponse<IReadOnlyList<MenuItemResponse>>>> BulkAvailability(Guid branchId, [FromBody] BulkAvailabilityRequest request)
        {
            return new ApiResponse<IReadOnlyList<MenuItemResponse>> { Result = await _menuManagementService.BulkUpdateAvailabilityAsync(branchId, request), Message = "Update availability successfully" };
        }

        [HttpPatch("api/owner/menu-items/{id:guid}/toggle-featured")]
        public async Task<ActionResult<ApiResponse<MenuItemResponse>>> ToggleFeatured(Guid id)
        {
            return new ApiResponse<MenuItemResponse> { Result = await _menuManagementService.ToggleMenuItemFeaturedAsync(id), Message = "Toggle featured successfully" };
        }

        [HttpPatch("api/owner/menu-items/{id:guid}/price")]
        public async Task<ActionResult<ApiResponse<MenuItemResponse>>> UpdatePrice(Guid id, [FromBody] UpdateMenuItemPriceRequest request)
        {
            return new ApiResponse<MenuItemResponse> { Result = await _menuManagementService.UpdateMenuItemPriceAsync(id, request), Message = "Update price successfully" };
        }

        [HttpGet("api/owner/menu-items/{id:guid}/price-history")]
        public async Task<ActionResult<ApiResponse<IReadOnlyList<PriceHistoryResponse>>>> GetPriceHistory(Guid id)
        {
            return new ApiResponse<IReadOnlyList<PriceHistoryResponse>> { Result = await _menuManagementService.GetManagePriceHistoryAsync(id), Message = "Get price history successfully" };
        }
    }
}
