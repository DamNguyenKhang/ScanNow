using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Exceptions;
using ScanNow.Application.Features.Auth;
using ScanNow.Application.Features.MenuManagement;
using ScanNow.Application.Features.RestaurantManagement;
using ScanNow.Application.Features.SessionUser;
using ScanNow.Application.Features.TableQr;
using ScanNow.Application.Features.UserManagement;
using System.Reflection;

namespace ScanNow.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddAutoMapper(Assembly.GetExecutingAssembly());

            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IUserManagementService, UserManagementService>();
            services.AddScoped<IRestaurantManagementService, RestaurantManagementService>();
            services.AddScoped<IMenuManagementService, MenuManagementService>();
            services.AddScoped<ITableQrService, TableQrService>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            return services;
        }
    }
}
