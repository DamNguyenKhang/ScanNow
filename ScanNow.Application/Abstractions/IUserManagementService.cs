using ScanNow.Application.Features.UserManagement.DTOs;

namespace ScanNow.Application.Abstractions
{
    public interface IUserManagementService
    {
        Task<PagedResult<OwnerUserResponse>> GetOwnersAsync(UserListQuery query);
        Task<OwnerUserResponse> CreateOwnerAsync(CreateOwnerRequest request);
        Task<OwnerUserResponse> UpdateOwnerAsync(Guid id, UpdateOwnerRequest request);
        Task BanOwnerAsync(Guid id, BanUserRequest request);
        Task UnbanOwnerAsync(Guid id);

        Task<PagedResult<OwnerScopedUserResponse>> GetOwnerUsersAsync(UserListQuery query);
        Task<OwnerScopedUserResponse> CreateOwnerUserAsync(CreateManagedUserRequest request);
        Task<OwnerScopedUserResponse> UpdateOwnerUserAsync(Guid id, UpdateManagedUserRequest request);
        Task BanOwnerUserAsync(Guid id);
        Task UnbanOwnerUserAsync(Guid id);

        Task<PagedResult<ManagerScopedUserResponse>> GetManagerUsersAsync(UserListQuery query);
        Task<ManagerScopedUserResponse> CreateManagerUserAsync(CreateManagedUserRequest request);
        Task<ManagerScopedUserResponse> UpdateManagerUserAsync(Guid id, UpdateManagedUserRequest request);
        Task BanManagerUserAsync(Guid id);
        Task UnbanManagerUserAsync(Guid id);
    }
}
