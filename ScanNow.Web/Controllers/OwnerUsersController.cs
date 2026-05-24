using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.UserManagement.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Route("api/owner/users")]
    [Authorize(Roles = nameof(UserRole.OWNER))]
    public class OwnerUsersController : ControllerBase
    {
        private readonly IUserManagementService _userManagementService;

        public OwnerUsersController(IUserManagementService userManagementService)
        {
            _userManagementService = userManagementService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResult<OwnerScopedUserResponse>>>> GetUsers([FromQuery] UserListQuery query)
        {
            return new ApiResponse<PagedResult<OwnerScopedUserResponse>>
            {
                Result = await _userManagementService.GetOwnerUsersAsync(query),
                Message = "Get users successfully"
            };
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<OwnerScopedUserResponse>>> CreateUser([FromBody] CreateManagedUserRequest request)
        {
            return new ApiResponse<OwnerScopedUserResponse>
            {
                Result = await _userManagementService.CreateOwnerUserAsync(request),
                Message = "Create user successfully"
            };
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ApiResponse<OwnerScopedUserResponse>>> UpdateUser(Guid id, [FromBody] UpdateManagedUserRequest request)
        {
            return new ApiResponse<OwnerScopedUserResponse>
            {
                Result = await _userManagementService.UpdateOwnerUserAsync(id, request),
                Message = "Update user successfully"
            };
        }

        [HttpPatch("{id:guid}/ban")]
        public async Task<ActionResult<ApiResponse>> BanUser(Guid id)
        {
            await _userManagementService.BanOwnerUserAsync(id);
            return new ApiResponse { Message = "Ban user successfully" };
        }

        [HttpPatch("{id:guid}/unban")]
        public async Task<ActionResult<ApiResponse>> UnbanUser(Guid id)
        {
            await _userManagementService.UnbanOwnerUserAsync(id);
            return new ApiResponse { Message = "Unban user successfully" };
        }
    }
}
