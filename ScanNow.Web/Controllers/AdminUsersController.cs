using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.UserManagement.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize(Roles = nameof(UserRole.ADMIN))]
    public class AdminUsersController : ControllerBase
    {
        private readonly IUserManagementService _userManagementService;

        public AdminUsersController(IUserManagementService userManagementService)
        {
            _userManagementService = userManagementService;
        }

        [HttpGet("owners")]
        public async Task<ActionResult<ApiResponse<PagedResult<OwnerUserResponse>>>> GetOwners([FromQuery] UserListQuery query)
        {
            return new ApiResponse<PagedResult<OwnerUserResponse>>
            {
                Result = await _userManagementService.GetOwnersAsync(query),
                Message = "Get owners successfully"
            };
        }

        [HttpGet("owners/available")]
        public async Task<ActionResult<ApiResponse<PagedResult<OwnerUserResponse>>>> GetAvailableOwners([FromQuery] UserListQuery query)
        {
            return new ApiResponse<PagedResult<OwnerUserResponse>>
            {
                Result = await _userManagementService.GetAvailableOwnersAsync(query),
                Message = "Get available owners successfully"
            };
        }

        [HttpPost("owners")]
        public async Task<ActionResult<ApiResponse<OwnerUserResponse>>> CreateOwner([FromBody] CreateOwnerRequest request)
        {
            return new ApiResponse<OwnerUserResponse>
            {
                Result = await _userManagementService.CreateOwnerAsync(request),
                Message = "Create owner successfully"
            };
        }

        [HttpPut("owners/{id:guid}")]
        public async Task<ActionResult<ApiResponse<OwnerUserResponse>>> UpdateOwner(Guid id, [FromBody] UpdateOwnerRequest request)
        {
            return new ApiResponse<OwnerUserResponse>
            {
                Result = await _userManagementService.UpdateOwnerAsync(id, request),
                Message = "Update owner successfully"
            };
        }

        [HttpPatch("owners/{id:guid}/ban")]
        public async Task<ActionResult<ApiResponse>> BanOwner(Guid id)
        {
            await _userManagementService.BanOwnerAsync(id, new BanUserRequest());
            return new ApiResponse { Message = "Ban owner successfully" };
        }

        [HttpPatch("owners/{id:guid}/unban")]
        public async Task<ActionResult<ApiResponse>> UnbanOwner(Guid id)
        {
            await _userManagementService.UnbanOwnerAsync(id);
            return new ApiResponse { Message = "Unban owner successfully" };
        }
    }
}
