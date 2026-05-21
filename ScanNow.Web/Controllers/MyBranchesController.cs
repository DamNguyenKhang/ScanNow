using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.RestaurantManagement.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Route("api/me/branches")]
    [Authorize(Roles = $"{nameof(UserRole.BRANCH_MANAGER)},{nameof(UserRole.STAFF)},{nameof(UserRole.KITCHEN)}")]
    public class MyBranchesController : ControllerBase
    {
        private readonly IRestaurantManagementService _restaurantManagementService;

        public MyBranchesController(IRestaurantManagementService restaurantManagementService)
        {
            _restaurantManagementService = restaurantManagementService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<IReadOnlyList<BranchResponse>>>> GetMyBranches()
        {
            return new ApiResponse<IReadOnlyList<BranchResponse>>
            {
                Result = await _restaurantManagementService.GetMyBranchesAsync(),
                Message = "Get my branches successfully"
            };
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResponse<BranchResponse>>> GetMyBranch(Guid id)
        {
            return new ApiResponse<BranchResponse>
            {
                Result = await _restaurantManagementService.GetMyBranchByIdAsync(id),
                Message = "Get my branch successfully"
            };
        }
    }
}
