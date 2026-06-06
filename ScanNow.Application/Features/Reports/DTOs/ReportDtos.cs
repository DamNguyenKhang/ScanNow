namespace ScanNow.Application.Features.Reports.DTOs
{
    public class ReportQuery
    {
        public Guid? BranchId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class ReportPointResponse
    {
        public string Label { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
        public decimal Revenue { get; set; }
        public int Orders { get; set; }
    }

    public class TopItemReportResponse
    {
        public Guid MenuItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
    }

    public class BranchReportResponse
    {
        public Guid BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int Orders { get; set; }
    }

    public class PaymentMethodReportResponse
    {
        public string Method { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Count { get; set; }
    }

    public class OwnerReportResponse
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal PaidRevenue { get; set; }
        public decimal PendingRevenue { get; set; }
        public int TotalOrders { get; set; }
        public int CompletedOrders { get; set; }
        public decimal AverageOrderValue { get; set; }
        public List<ReportPointResponse> RevenueByDay { get; set; } = [];
        public List<ReportPointResponse> PeakHours { get; set; } = [];
        public List<TopItemReportResponse> TopItems { get; set; } = [];
        public List<BranchReportResponse> Branches { get; set; } = [];
        public List<PaymentMethodReportResponse> PaymentMethods { get; set; } = [];
    }

    public class AdminDashboardReportResponse
    {
        public int TotalUsers { get; set; }
        public int TotalRestaurants { get; set; }
        public int TotalBranches { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<ReportPointResponse> PlatformGrowth { get; set; } = [];
        public List<ReportPointResponse> RevenueByMonth { get; set; } = [];
    }
}
