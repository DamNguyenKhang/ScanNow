using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.UserManagement.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Route("api/manager/users")]
    [Authorize(Roles = nameof(UserRole.BRANCH_MANAGER))]
    public class ManagerUsersController : ControllerBase
    {
        private readonly IUserManagementService _userManagementService;

        public ManagerUsersController(IUserManagementService userManagementService)
        {
            _userManagementService = userManagementService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResult<ManagerScopedUserResponse>>>> GetUsers([FromQuery] UserListQuery query)
        {
            return new ApiResponse<PagedResult<ManagerScopedUserResponse>>
            {
                Result = await _userManagementService.GetManagerUsersAsync(query),
                Message = "Get users successfully"
            };
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<ManagerScopedUserResponse>>> CreateUser([FromBody] CreateManagedUserRequest request)
        {
            return new ApiResponse<ManagerScopedUserResponse>
            {
                Result = await _userManagementService.CreateManagerUserAsync(request),
                Message = "Create user successfully"
            };
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ApiResponse<ManagerScopedUserResponse>>> UpdateUser(Guid id, [FromBody] UpdateManagedUserRequest request)
        {
            return new ApiResponse<ManagerScopedUserResponse>
            {
                Result = await _userManagementService.UpdateManagerUserAsync(id, request),
                Message = "Update user successfully"
            };
        }

        [HttpPatch("{id:guid}/ban")]
        public async Task<ActionResult<ApiResponse>> BanUser(Guid id)
        {
            await _userManagementService.BanManagerUserAsync(id);
            return new ApiResponse { Message = "Ban user successfully" };
        }

        [HttpPatch("{id:guid}/unban")]
        public async Task<ActionResult<ApiResponse>> UnbanUser(Guid id)
        {
            await _userManagementService.UnbanManagerUserAsync(id);
            return new ApiResponse { Message = "Unban user successfully" };
        }
    }
}
