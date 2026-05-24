namespace ScanNow.Application.Features.MenuManagement.DTOs
{
    public class MenuQuery
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsAvailable { get; set; }
        public bool? IsFeatured { get; set; }
        public Guid? CategoryId { get; set; }
        public string? SortBy { get; set; }
        public string? SortDirection { get; set; } = "asc";
    }

    public class CategoryQuery
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public bool? IsActive { get; set; }
        public string? SortBy { get; set; }
        public string? SortDirection { get; set; } = "asc";
    }

    public class CreateCategoryRequest
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class UpdateCategoryRequest
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class ReorderCategoryRequest
    {
        public List<ReorderItemRequest> Items { get; set; } = new();
    }

    public class CreateMenuItemRequest
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public decimal Price { get; set; }
        public decimal CostPrice { get; set; }
        public int PreparationTime { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsAvailable { get; set; } = true;
        public bool IsFeatured { get; set; }
    }

    public class UpdateMenuItemRequest
    {
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public decimal CostPrice { get; set; }
        public int PreparationTime { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsFeatured { get; set; }
    }

    public class ReorderMenuItemRequest
    {
        public List<ReorderItemRequest> Items { get; set; } = new();
    }

    public class ReorderItemRequest
    {
        public Guid Id { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class UpdateMenuItemPriceRequest
    {
        public decimal Price { get; set; }
        public string? Note { get; set; }
    }

    public class BulkAvailabilityRequest
    {
        public bool IsAvailable { get; set; }
        public List<Guid> MenuItemIds { get; set; } = new();
    }

    public class CategoryResponse
    {
        public Guid CategoryId { get; set; }
        public Guid BranchId { get; set; }
        public string? BranchName { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class MenuItemResponse
    {
        public Guid MenuItemId { get; set; }
        public Guid BranchId { get; set; }
        public string? BranchName { get; set; }
        public Guid CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public decimal Price { get; set; }
        public decimal CostPrice { get; set; }
        public int PreparationTime { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class MenuCategoryResponse
    {
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public IReadOnlyList<MenuItemResponse> Items { get; set; } = Array.Empty<MenuItemResponse>();
    }

    public class PriceHistoryResponse
    {
        public Guid PriceHistoryId { get; set; }
        public Guid MenuItemId { get; set; }
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public Guid ChangedById { get; set; }
        public string? ChangedByName { get; set; }
        public DateTime ChangedAt { get; set; }
        public string? Note { get; set; }
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
