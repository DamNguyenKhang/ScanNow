using Microsoft.AspNetCore.Authentication.JwtBearer;
using ScanNow.Application.Exceptions;

namespace ScanNow.Web.Configurations
{
    public class JwtEvents : JwtBearerEvents
    {
        /// <summary>
        /// Log lý do token fail để debug
        /// </summary>
        public override Task AuthenticationFailed(AuthenticationFailedContext context)
        {
            Console.WriteLine($"[JWT] Authentication failed: {context.Exception.GetType().Name}: {context.Exception.Message}");
            return base.AuthenticationFailed(context);
        }

        /// <summary>
        /// Khi chưa login hoặc token invalid → throw UnauthorizedException (401)
        /// </summary>
        public override Task Challenge(JwtBearerChallengeContext context)
        {
            if (context.AuthenticateFailure != null)
            {
                Console.WriteLine($"[JWT] Challenge reason: {context.AuthenticateFailure.GetType().Name}: {context.AuthenticateFailure.Message}");
            }
            context.HandleResponse();
            throw new UnauthorizedException();
        }

        /// <summary>
        /// Khi không có quyền → throw ForbiddenException (403)
        /// </summary>
        public override Task Forbidden(ForbiddenContext context)
        {
            throw new ForbiddenException();
        }
    }
}
