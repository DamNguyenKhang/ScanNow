using ScanNow.Application.Abstractions;
using ScanNow.Application.Exceptions;
using ScanNow.Application.Features.Reports.DTOs;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;
using ScanNow.Domain.Exceptions;
using OrderEntity = ScanNow.Domain.Entities.Order;

namespace ScanNow.Application.Features.Reports
{
    public class ReportService : IReportService
    {
        private static readonly string OwnerRole = UserRole.OWNER.ToString();
        private static readonly string BranchManagerRole = UserRole.BRANCH_MANAGER.ToString();
        private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

        private readonly IReportRepository _repository;
        private readonly ICurrentUserService _currentUserService;

        public ReportService(IReportRepository repository, ICurrentUserService currentUserService)
        {
            _repository = repository;
            _currentUserService = currentUserService;
        }

        public Task<OwnerReportResponse> GetOwnerReportAsync(ReportQuery query)
        {
            return GetScopedReportAsync(query, OwnerRole, ownerScope: true);
        }

        public Task<OwnerReportResponse> GetBranchManagerReportAsync(ReportQuery query)
        {
            return GetScopedReportAsync(query, BranchManagerRole, ownerScope: false);
        }

        public async Task<AdminDashboardReportResponse> GetAdminDashboardAsync()
        {
            var from = FirstDayOfMonth(DateTime.UtcNow.AddMonths(-11));
            var restaurants = await _repository.GetRestaurantsCreatedSinceAsync(from);
            var users = await _repository.GetUsersCreatedSinceAsync(from);
            var orders = await _repository.GetOrdersCreatedSinceAsync(from);

            return new AdminDashboardReportResponse
            {
                TotalUsers = await _repository.CountUsersAsync(),
                TotalRestaurants = await _repository.CountRestaurantsAsync(),
                TotalBranches = await _repository.CountBranchesAsync(),
                TotalOrders = await _repository.CountOrdersAsync(),
                TotalRevenue = orders.Where(HasSuccessfulPayment).Sum(x => x.TotalAmount),
                PlatformGrowth = Enumerable.Range(0, 12)
                    .Select(offset => FirstDayOfMonth(from.AddMonths(offset)))
                    .Select(month => new ReportPointResponse
                    {
                        Label = month.ToString("MMM yyyy"),
                        Date = month,
                        Orders = restaurants.Count(x => FirstDayOfMonth(x.CreatedAt) == month)
                                 + users.Count(x => FirstDayOfMonth(x.CreatedAt) == month)
                    })
                    .ToList(),
                RevenueByMonth = Enumerable.Range(0, 12)
                    .Select(offset => FirstDayOfMonth(from.AddMonths(offset)))
                    .Select(month =>
                    {
                        var monthOrders = orders.Where(x => FirstDayOfMonth(x.CreatedAt) == month).ToList();
                        return new ReportPointResponse
                        {
                            Label = month.ToString("MMM yyyy"),
                            Date = month,
                            Revenue = monthOrders.Where(HasSuccessfulPayment).Sum(x => x.TotalAmount),
                            Orders = monthOrders.Count
                        };
                    })
                    .ToList()
            };
        }

        private async Task<OwnerReportResponse> GetScopedReportAsync(ReportQuery query, string requiredRole, bool ownerScope)
        {
            var userId = _currentUserService.UserId ?? throw new UnauthorizedException();
            if (_currentUserService.Role != requiredRole)
            {
                throw new ForbiddenException();
            }

            var branches = await _repository.GetManageableBranchesAsync(userId, ownerScope);
            if (branches.Count == 0)
            {
                throw new BusinessRuleException(ownerScope ? "Owner has no branch" : "Manager has no branch");
            }

            if (query.BranchId.HasValue)
            {
                branches = branches.Where(x => x.Id == query.BranchId.Value).ToList();
                if (branches.Count == 0)
                {
                    throw new ForbiddenException();
                }
            }

            var (from, to) = NormalizeRange(query);
            var orders = await _repository.GetOrdersForBranchesAsync(branches.Select(x => x.Id), from, to);
            return BuildOwnerReport(branches, orders, from, to);
        }

        private static OwnerReportResponse BuildOwnerReport(List<Branch> branches, List<OrderEntity> orders, DateTime from, DateTime to)
        {
            var paidOrders = orders.Where(HasSuccessfulPayment).ToList();
            var pendingOrders = orders.Where(x => !HasSuccessfulPayment(x) && x.Status != OrderStatus.Cancelled).ToList();
            var totalRevenue = orders.Where(x => x.Status != OrderStatus.Cancelled).Sum(x => x.TotalAmount);
            var paidRevenue = paidOrders.Sum(x => x.TotalAmount);
            var completedOrders = orders.Count(x => x.Status == OrderStatus.Completed || HasSuccessfulPayment(x));

            return new OwnerReportResponse
            {
                FromDate = TimeZoneInfo.ConvertTimeFromUtc(from, VietnamTimeZone),
                ToDate = TimeZoneInfo.ConvertTimeFromUtc(to.AddTicks(-1), VietnamTimeZone),
                TotalRevenue = totalRevenue,
                PaidRevenue = paidRevenue,
                PendingRevenue = pendingOrders.Sum(x => x.TotalAmount),
                TotalOrders = orders.Count,
                CompletedOrders = completedOrders,
                AverageOrderValue = orders.Count == 0 ? 0 : totalRevenue / orders.Count,
                RevenueByDay = BuildRevenueByDay(orders, from, to),
                PeakHours = BuildPeakHours(orders),
                TopItems = BuildTopItems(orders),
                PaymentMethods = BuildPaymentMethods(orders),
                Branches = branches
                    .Select(branch =>
                    {
                        var branchOrders = orders.Where(x => x.BranchId == branch.Id).ToList();
                        return new BranchReportResponse
                        {
                            BranchId = branch.Id,
                            BranchName = branch.Name,
                            Revenue = branchOrders.Where(HasSuccessfulPayment).Sum(x => x.TotalAmount),
                            Orders = branchOrders.Count
                        };
                    })
                    .OrderByDescending(x => x.Revenue)
                    .ToList()
            };
        }

        private static List<ReportPointResponse> BuildRevenueByDay(List<OrderEntity> orders, DateTime from, DateTime to)
        {
            var days = new List<ReportPointResponse>();
            var fromLocal = TimeZoneInfo.ConvertTimeFromUtc(from, VietnamTimeZone).Date;
            var toLocal = TimeZoneInfo.ConvertTimeFromUtc(to, VietnamTimeZone).Date;

            for (var day = fromLocal; day < toLocal; day = day.AddDays(1))
            {
                var nextDay = day.AddDays(1);
                var dayStartUtc = ToVietnamLocalDateUtc(day);
                var dayEndUtc = ToVietnamLocalDateUtc(nextDay);
                var dayOrders = orders.Where(x => AsUtc(x.CreatedAt) >= dayStartUtc && AsUtc(x.CreatedAt) < dayEndUtc).ToList();
                days.Add(new ReportPointResponse
                {
                    Label = day.ToString("dd/MM"),
                    Date = day,
                    Revenue = dayOrders.Where(HasSuccessfulPayment).Sum(x => x.TotalAmount),
                    Orders = dayOrders.Count
                });
            }

            return days;
        }

        private static List<ReportPointResponse> BuildPeakHours(List<OrderEntity> orders)
        {
            return Enumerable.Range(0, 24)
                .Select(hour =>
                {
                    var hourOrders = orders
                        .Where(x => TimeZoneInfo.ConvertTimeFromUtc(AsUtc(x.CreatedAt), VietnamTimeZone).Hour == hour)
                        .ToList();
                    return new ReportPointResponse
                    {
                        Label = $"{hour:00}:00",
                        Orders = hourOrders.Count,
                        Revenue = hourOrders.Where(HasSuccessfulPayment).Sum(x => x.TotalAmount)
                    };
                })
                .ToList();
        }

        private static List<TopItemReportResponse> BuildTopItems(List<OrderEntity> orders)
        {
            return orders
                .SelectMany(x => x.Items)
                .GroupBy(x => new { x.MenuItemId, x.MenuItemName })
                .Select(group => new TopItemReportResponse
                {
                    MenuItemId = group.Key.MenuItemId,
                    Name = group.Key.MenuItemName,
                    Quantity = group.Sum(x => x.Quantity),
                    Revenue = group.Sum(x => x.SubTotal)
                })
                .OrderByDescending(x => x.Quantity)
                .ThenByDescending(x => x.Revenue)
                .Take(10)
                .ToList();
        }

        private static List<PaymentMethodReportResponse> BuildPaymentMethods(List<OrderEntity> orders)
        {
            return orders
                .SelectMany(x => x.Payments)
                .Where(x => x.Status == PaymentStatus.SUCCESS)
                .GroupBy(x => x.Method)
                .Select(group => new PaymentMethodReportResponse
                {
                    Method = group.Key.ToString(),
                    Amount = group.Sum(x => x.Amount),
                    Count = group.Count()
                })
                .OrderByDescending(x => x.Amount)
                .ToList();
        }

        private static bool HasSuccessfulPayment(OrderEntity order)
        {
            return order.Payments.Any(x => x.Status == PaymentStatus.SUCCESS);
        }

        private static (DateTime From, DateTime To) NormalizeRange(ReportQuery query)
        {
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
            var from = (query.FromDate ?? now.Date.AddDays(-29)).Date;
            var to = (query.ToDate ?? now.Date).Date.AddDays(1);

            if (to <= from)
            {
                to = from.AddDays(1);
            }

            return (ToVietnamLocalDateUtc(from), ToVietnamLocalDateUtc(to));
        }

        private static DateTime FirstDayOfMonth(DateTime value)
        {
            return new DateTime(value.Year, value.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        private static DateTime ToVietnamLocalDateUtc(DateTime localDate)
        {
            var unspecified = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, VietnamTimeZone);
        }

        private static DateTime AsUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static TimeZoneInfo ResolveVietnamTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
        }
    }
}
