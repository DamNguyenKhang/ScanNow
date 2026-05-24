using Microsoft.AspNetCore.Http;
using ScanNow.Application.Abstractions;
using System.Security.Claims;

namespace ScanNow.Application.Features.SessionUser
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? UserId
        {
            get
            {
                var userIdClaim = _httpContextAccessor.HttpContext?.User?
                    .FindFirstValue(ClaimTypes.NameIdentifier);

                return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
            }
        }

        public string? Email => _httpContextAccessor.HttpContext?.User?
            .FindFirstValue(ClaimTypes.Email);

        public string? Role => _httpContextAccessor.HttpContext?.User?
            .FindFirstValue(ClaimTypes.Role)
            ?? _httpContextAccessor.HttpContext?.User?
                .FindFirstValue("role");

        //public Guid? BranchId
        //{
        //    get
        //    {
        //        var branchIdClaim = _httpContextAccessor.HttpContext?.User?
        //            .FindFirstValue("BranchId");

        //        return Guid.TryParse(branchIdClaim, out var branchId) ? branchId : null;
        //    }
        //}

        public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

        //public bool IsInRole(string role)
        //{
        //    return _httpContextAccessor.HttpContext?.User?.IsInRole(role) ?? false;
        //}

        //public bool IsInAnyRole(params string[] roles)
        //{
        //    return roles.Any(role => IsInRole(role));
        //}
    }

}
