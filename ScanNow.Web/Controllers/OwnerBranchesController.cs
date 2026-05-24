using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.RestaurantManagement.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Route("api/owner/branches")]
    [Authorize(Roles = nameof(UserRole.OWNER))]
    public class OwnerBranchesController : ControllerBase
    {
        private readonly IRestaurantManagementService _restaurantManagementService;

        public OwnerBranchesController(IRestaurantManagementService restaurantManagementService)
        {
            _restaurantManagementService = restaurantManagementService;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<BranchResponse>>> CreateBranch([FromBody] CreateBranchRequest request)
        {
            return new ApiResponse<BranchResponse>
            {
                Result = await _restaurantManagementService.CreateOwnerBranchAsync(request),
                Message = "Create branch successfully"
            };
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResult<BranchResponse>>>> GetBranches([FromQuery] BranchQuery query)
        {
            return new ApiResponse<PagedResult<BranchResponse>>
            {
                Result = await _restaurantManagementService.GetOwnerBranchesAsync(query),
                Message = "Get branches successfully"
            };
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResponse<BranchResponse>>> GetBranch(Guid id)
        {
            return new ApiResponse<BranchResponse>
            {
                Result = await _restaurantManagementService.GetOwnerBranchByIdAsync(id),
                Message = "Get branch successfully"
            };
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ApiResponse<BranchResponse>>> UpdateBranch(Guid id, [FromBody] UpdateBranchRequest request)
        {
            return new ApiResponse<BranchResponse>
            {
                Result = await _restaurantManagementService.UpdateOwnerBranchAsync(id, request),
                Message = "Update branch successfully"
            };
        }

        [HttpPatch("{id:guid}/inactive")]
        public async Task<ActionResult<ApiResponse<BranchResponse>>> InactiveBranch(Guid id)
        {
            return new ApiResponse<BranchResponse>
            {
                Result = await _restaurantManagementService.InactiveOwnerBranchAsync(id),
                Message = "Inactive branch successfully"
            };
        }

        [HttpPatch("{id:guid}/active")]
        public async Task<ActionResult<ApiResponse<BranchResponse>>> ActiveBranch(Guid id)
        {
            return new ApiResponse<BranchResponse>
            {
                Result = await _restaurantManagementService.ActiveOwnerBranchAsync(id),
                Message = "Active branch successfully"
            };
        }
    }
}
