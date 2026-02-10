using Microsoft.AspNetCore.Authentication.JwtBearer;
using ScanNow.Application.Exceptions;

namespace ScanNow.Web.Configurations
{
    public class JwtEvents : JwtBearerEvents
    {
        /// <summary>
        /// Khi chưa login hoặc token invalid → throw UnauthorizedException (401)
        /// </summary>
        public override Task Challenge(JwtBearerChallengeContext context)
        {
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
