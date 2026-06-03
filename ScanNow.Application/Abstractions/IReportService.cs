using ScanNow.Application.Features.Reports.DTOs;

namespace ScanNow.Application.Abstractions
{
    public interface IReportService
    {
        Task<OwnerReportResponse> GetOwnerReportAsync(ReportQuery query);
        Task<OwnerReportResponse> GetBranchManagerReportAsync(ReportQuery query);
        Task<AdminDashboardReportResponse> GetAdminDashboardAsync();
    }
}
