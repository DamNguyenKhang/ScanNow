using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text.Json;

namespace ScanNow.Web.Configurations
{
    public class JwtEvents : JwtBearerEvents
    {
        public override Task AuthenticationFailed(AuthenticationFailedContext context)
        {
            Console.WriteLine($"[JWT] Authentication failed: {context.Exception.GetType().Name}: {context.Exception.Message}");
            return base.AuthenticationFailed(context);
        }

        public override Task Challenge(JwtBearerChallengeContext context)
        {
            if (context.AuthenticateFailure != null)
            {
                Console.WriteLine($"[JWT] Challenge reason: {context.AuthenticateFailure.GetType().Name}: {context.AuthenticateFailure.Message}");
            }

            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";

            var payload = JsonSerializer.Serialize(new
            {
                status = StatusCodes.Status401Unauthorized,
                title = "Unauthorized",
                detail = "Unauthenticated.",
                type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                instance = context.Request.Path.Value
            });

            return context.Response.WriteAsync(payload);
        }

        public override Task Forbidden(ForbiddenContext context)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";

            var payload = JsonSerializer.Serialize(new
            {
                status = StatusCodes.Status403Forbidden,
                title = "Forbidden",
                detail = "You don't have permission to perform this action.",
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                instance = context.Request.Path.Value
            });

            return context.Response.WriteAsync(payload);
        }
    }
}
