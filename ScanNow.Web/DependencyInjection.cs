using ScanNow.Web.ExceptionHandler;

namespace ScanNow.Web
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddPresentation(this IServiceCollection services)
        {
            services.AddScoped<ScanNow.Application.Abstractions.IOrderUpdatePublisher, ScanNow.Web.Hubs.SignalROrderUpdatePublisher>();
            services.AddProblemDetails(configure =>
                configure.CustomizeProblemDetails = context =>
                {
                    context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
                }
            );
            return services;
        }

        public static IServiceCollection AddExceptionHandler(this IServiceCollection services)
        {
            services.AddExceptionHandler<ValidationExceptionHandler>();
            services.AddExceptionHandler<GlobalExceptionHandler>();
            return services;
        }
    }
}
