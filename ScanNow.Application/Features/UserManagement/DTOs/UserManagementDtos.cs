namespace ScanNow.Application.Features.UserManagement.DTOs
{
    public class UserListQuery
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public string? Role { get; set; }
        public Guid? BranchId { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsBanned { get; set; }
        public string? SortBy { get; set; }
        public string? SortDirection { get; set; } = "asc";
    }

    public class CreateManagedUserRequest
    {
        public string FullName { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string Password { get; set; } = null!;
        public string Role { get; set; } = null!;
        public List<Guid> BranchIds { get; set; } = new();
    }

    public class UpdateManagedUserRequest
    {
        public string FullName { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = null!;
        public List<Guid> BranchIds { get; set; } = new();
    }

    public class CreateOwnerRequest
    {
        public string FullName { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string Password { get; set; } = null!;
    }

    public class UpdateOwnerRequest
    {
        public string FullName { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? PhoneNumber { get; set; }
    }

    public class BanUserRequest
    {
        public string? Reason { get; set; }
    }

    public class OwnerUserResponse
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; }
        public bool IsBanned { get; set; }
        public Guid? RestaurantId { get; set; }
        public string? RestaurantName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class OwnerScopedUserResponse
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = string.Empty;
        public Guid RestaurantId { get; set; }
        public string RestaurantName { get; set; } = string.Empty;
        public List<Guid> BranchIds { get; set; } = new();
        public List<string> BranchNames { get; set; } = new();
        public bool IsActive { get; set; }
        public bool IsBanned { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ManagerScopedUserResponse
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = string.Empty;
        public List<Guid> BranchIds { get; set; } = new();
        public List<string> BranchNames { get; set; } = new();
        public bool IsActive { get; set; }
        public bool IsBanned { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalItems / PageSize);
    }
}
