namespace ScanNow.Application.Features.RestaurantManagement.DTOs
{
    public class CreateRestaurantRequest
    {
        public Guid OwnerId { get; set; }
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? LogoUrl { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateRestaurantRequest
    {
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? LogoUrl { get; set; }
        public string? Description { get; set; }
    }

    public class RestaurantResponse
    {
        public Guid RestaurantId { get; set; }
        public Guid OwnerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class RestaurantQuery
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public bool? IsActive { get; set; }
        public string? SortBy { get; set; }
        public string? SortDirection { get; set; } = "asc";
    }

    public class CreateBranchRequest
    {
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public TimeOnly? OpenTime { get; set; }
        public TimeOnly? CloseTime { get; set; }
        public decimal VatPercent { get; set; }
        public decimal ServiceChargePercent { get; set; }
        public decimal ServiceChargeFixed { get; set; }
    }

    public class UpdateBranchRequest
    {
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public TimeOnly? OpenTime { get; set; }
        public TimeOnly? CloseTime { get; set; }
        public decimal VatPercent { get; set; }
        public decimal ServiceChargePercent { get; set; }
        public decimal ServiceChargeFixed { get; set; }
    }

    public class BranchQuery
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public bool? IsActive { get; set; }
        public string? SortBy { get; set; }
        public string? SortDirection { get; set; } = "asc";
    }

    public class BranchResponse
    {
        public Guid BranchId { get; set; }
        public Guid RestaurantId { get; set; }
        public Guid? ManagerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public TimeOnly? OpenTime { get; set; }
        public TimeOnly? CloseTime { get; set; }
        public bool IsActive { get; set; }
        public decimal VatPercent { get; set; }
        public decimal ServiceChargePercent { get; set; }
        public decimal ServiceChargeFixed { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
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
