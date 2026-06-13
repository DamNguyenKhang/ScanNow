using ScanNow.Application.Features.MenuManagement.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Application.Features.TableQr.DTOs
{
    public class TableQuery
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public TableStatus? Status { get; set; }
        public int? Capacity { get; set; }
        public string? Search { get; set; }
        public bool? IsActive { get; set; }
        public string? SortBy { get; set; }
        public string? SortDirection { get; set; } = "asc";
    }

    public class CreateTableRequest
    {
        public string TableNumber { get; set; } = null!;
        public int Capacity { get; set; } = 4;
    }

    public class UpdateTableRequest
    {
        public string TableNumber { get; set; } = null!;
        public int Capacity { get; set; } = 4;
    }

    public class UpdateTableStatusRequest
    {
        public TableStatus Status { get; set; }
    }

    public class JoinSessionRequest
    {
        public string SessionCode { get; set; } = null!;
    }

    public class TableResponse
    {
        public Guid TableId { get; set; }
        public Guid BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string TableNumber { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public string QrCodeToken { get; set; } = string.Empty;
        public string? QrCodeUrl { get; set; }
        public string? QrCodeImageUrl { get; set; }
        public TableStatus Status { get; set; }
        public bool IsActive { get; set; }
        public QrSessionResponse? CurrentSession { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class PublicTableResponse
    {
        public Guid TableId { get; set; }
        public Guid BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string TableNumber { get; set; } = string.Empty;
        public TableStatus Status { get; set; }
    }

    public class QrSessionResponse
    {
        public Guid SessionId { get; set; }
        public Guid TableId { get; set; }
        public Guid BranchId { get; set; }
        public string SessionCode { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class JoinSessionResponse
    {
        public Guid SessionId { get; set; }
        public Guid TableId { get; set; }
        public Guid BranchId { get; set; }
        public string SessionCode { get; set; } = string.Empty;
        public string TableNumber { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    public class SessionMenuResponse
    {
        public JoinSessionResponse Session { get; set; } = null!;
        public ScanNow.Application.Features.MenuManagement.DTOs.PagedResult<MenuCategoryResponse> Menu { get; set; } = null!;
    }
}
