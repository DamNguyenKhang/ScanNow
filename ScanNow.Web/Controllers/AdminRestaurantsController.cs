using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.RestaurantManagement.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Route("api/admin/restaurants")]
    [Authorize(Roles = nameof(UserRole.ADMIN))]
    public class AdminRestaurantsController : ControllerBase
    {
        private readonly IRestaurantManagementService _restaurantManagementService;

        public AdminRestaurantsController(IRestaurantManagementService restaurantManagementService)
        {
            _restaurantManagementService = restaurantManagementService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResult<RestaurantResponse>>>> GetRestaurants([FromQuery] RestaurantQuery query)
        {
            return new ApiResponse<PagedResult<RestaurantResponse>>
            {
                Result = await _restaurantManagementService.GetRestaurantsAsync(query),
                Message = "Get restaurants successfully"
            };
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResponse<RestaurantResponse>>> GetRestaurant(Guid id)
        {
            return new ApiResponse<RestaurantResponse>
            {
                Result = await _restaurantManagementService.GetRestaurantByIdAsync(id),
                Message = "Get restaurant successfully"
            };
        }

        [HttpGet("{id:guid}/branches")]
        public async Task<ActionResult<ApiResponse<PagedResult<BranchResponse>>>> GetRestaurantBranches(Guid id, [FromQuery] BranchQuery query)
        {
            return new ApiResponse<PagedResult<BranchResponse>>
            {
                Result = await _restaurantManagementService.GetRestaurantBranchesAsync(id, query),
                Message = "Get restaurant branches successfully"
            };
        }

        [HttpGet("{id:guid}/branches/{branchId:guid}")]
        public async Task<ActionResult<ApiResponse<BranchResponse>>> GetRestaurantBranch(Guid id, Guid branchId)
        {
            return new ApiResponse<BranchResponse>
            {
                Result = await _restaurantManagementService.GetRestaurantBranchByIdAsync(id, branchId),
                Message = "Get restaurant branch successfully"
            };
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<RestaurantResponse>>> CreateRestaurant([FromBody] CreateRestaurantRequest request)
        {
            return new ApiResponse<RestaurantResponse>
            {
                Result = await _restaurantManagementService.CreateRestaurantAsync(request),
                Message = "Create restaurant successfully"
            };
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ApiResponse<RestaurantResponse>>> UpdateRestaurant(Guid id, [FromBody] UpdateRestaurantRequest request)
        {
            return new ApiResponse<RestaurantResponse>
            {
                Result = await _restaurantManagementService.UpdateRestaurantAsync(id, request),
                Message = "Update restaurant successfully"
            };
        }

        [HttpPatch("{id:guid}/ban")]
        public async Task<ActionResult<ApiResponse<RestaurantResponse>>> BanRestaurant(Guid id)
        {
            return new ApiResponse<RestaurantResponse>
            {
                Result = await _restaurantManagementService.BanRestaurantAsync(id),
                Message = "Ban restaurant successfully"
            };
        }

        [HttpPatch("{id:guid}/unban")]
        public async Task<ActionResult<ApiResponse<RestaurantResponse>>> UnbanRestaurant(Guid id)
        {
            return new ApiResponse<RestaurantResponse>
            {
                Result = await _restaurantManagementService.UnbanRestaurantAsync(id),
                Message = "Unban restaurant successfully"
            };
        }
    }
}
