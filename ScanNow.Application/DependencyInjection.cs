using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Exceptions;
using ScanNow.Application.Features.Auth;
using ScanNow.Application.Features.Cashier;
using ScanNow.Application.Features.Checkout;
using ScanNow.Application.Features.Kitchen;
using ScanNow.Application.Features.MenuManagement;
using ScanNow.Application.Features.Order;
using ScanNow.Application.Features.RestaurantManagement;
using ScanNow.Application.Features.SessionUser;
using ScanNow.Application.Features.TableQr;
using ScanNow.Application.Features.UserManagement;
using ScanNow.Application.Features.Cart;
using ScanNow.Application.Features.Waiter;
using ScanNow.Application.Features.BranchSettings;
using ScanNow.Application.Features.Reports;
using ScanNow.Application.Features.Common;
using System.Reflection;

namespace ScanNow.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddAutoMapper(Assembly.GetExecutingAssembly());

            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            services.AddScoped<ITenantUrlBuilder, TenantUrlBuilder>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IUserManagementService, UserManagementService>();
            services.AddScoped<IRestaurantManagementService, RestaurantManagementService>();
            services.AddScoped<IMenuManagementService, MenuManagementService>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<ICashierService, CashierService>();
            services.AddScoped<ICheckoutService, CheckoutService>();
            services.AddScoped<ITableQrService, TableQrService>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<ICartService, CartService>();
            services.AddScoped<IWaiterService, WaiterService>();
            services.AddScoped<IKitchenService, KitchenService>();
            services.AddScoped<IBranchSettingsService, BranchSettingsService>();
            services.AddScoped<IReportService, ReportService>();
            return services;
        }
    }
}
