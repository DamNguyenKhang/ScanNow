using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.Auth.DTOs.Requests;
using ScanNow.Application.Features.Auth.DTOs.Response;

namespace ScanNow.Web.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private const string RefreshTokenCookieName = "refreshToken";
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;

        public AuthController(IAuthService authService, IConfiguration configuration)
        {
            _authService = authService;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<ActionResult<ApiResponse<UserResponse>>> Register([FromBody] SignUpUserRequest request)
        {
            return new ApiResponse<UserResponse>
            {
                Result = await _authService.RegisterAsync(request),
                Message = "Create new user successfully"
            };
        }

        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] AuthRequest request)
        {
            var authResponse = await _authService.LoginAsync(request);
            SetRefreshTokenCookie(authResponse.RefreshToken);

            return new ApiResponse<AuthResponse>
            {
                Result = authResponse,
                Message = "Login successfully"
            };
        }

        [HttpPost("login-google")]
        public async Task<ActionResult<ApiResponse<AuthResponse>>> LoginGoogle([FromBody] GoogleLoginRequest request)
        {
            var authResponse = await _authService.LoginGoogleAsync(request);
            SetRefreshTokenCookie(authResponse.RefreshToken);

            return new ApiResponse<AuthResponse>
            {
                Result = authResponse,
                Message = "Login successfully"
            };
        }

        [HttpPost("refresh-token")]
        public async Task<ActionResult<ApiResponse<AuthResponse>>> RefreshToken()
        {
            if (!Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
            {
                return Unauthorized("Refresh token missing");
            }

            return new ApiResponse<AuthResponse>
            {
                Result = await _authService.RefreshTokenAsync(refreshToken),
                Message = "Refresh token successfully"
            };
        }

        [HttpPost("logout")]
        public async Task<ActionResult<ApiResponse>> Logout()
        {
            if (!Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
            {
                return Unauthorized("Refresh token missing");
            }

            await _authService.LogoutAsync(refreshToken);
            ExpireRefreshTokenCookie();

            return new ApiResponse
            {
                Message = "Logout successfully"
            };
        }

        [HttpPost("check-email-exist")]
        public async Task<ActionResult<ApiResponse<bool>>> CheckEmailExists([FromBody] CheckEmailRequest request)
        {
            var exists = await _authService.CheckEmailExistsAsync(request);
            return new ApiResponse<bool>
            {
                Result = exists,
                Message = exists ? "Email exists" : "Email does not exist"
            };
        }

        [HttpPost("check-username-exist")]
        public async Task<ActionResult<ApiResponse<bool>>> CheckUsernameExists([FromBody] CheckUsernameRequest request)
        {
            var exists = await _authService.CheckUsernameExistsAsync(request);
            return new ApiResponse<bool>
            {
                Result = exists,
                Message = exists ? "Username exists" : "Username does not exist"
            };
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<ActionResult<ApiResponse<bool>>> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var isSuccess = await _authService.ChangePasswordAsync(request);
            return new ApiResponse<bool>
            {
                Result = isSuccess,
                Message = isSuccess ? "Change password successfully" : "Change password failed"
            };
        }

        [HttpPost("resend-email-verification")]
        public async Task<ActionResult<ApiResponse>> ResendEmailVerification([FromBody] ResendEmailRequest request)
        {
            await _authService.ResendEmailVerificationAsync(request);
            return new ApiResponse
            {
                Message = "Resend email verification successfully"
            };
        }

        [HttpPost("verify-email")]
        public async Task<ActionResult<ApiResponse<AuthResponse>>> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            var authResponse = await _authService.VerifyEmailAsync(request);
            SetRefreshTokenCookie(authResponse.RefreshToken);

            return new ApiResponse<AuthResponse>
            {
                Result = authResponse,
                Message = "Verify email successfully"
            };
        }

        private void SetRefreshTokenCookie(string? refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return;
            }

            Response.Cookies.Append(RefreshTokenCookieName, refreshToken, CreateCookieOptions());
        }

        private void ExpireRefreshTokenCookie()
        {
            var options = CreateCookieOptions();
            options.Expires = DateTimeOffset.UtcNow.AddDays(-1);
            Response.Cookies.Append(RefreshTokenCookieName, string.Empty, options);
        }

        private CookieOptions CreateCookieOptions()
        {
            var refreshTokenLifetime = TimeSpan.FromDays(_configuration.GetValue<double>("Jwt:RefreshTokenExpirationDays"));

            return new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.Add(refreshTokenLifetime),
                MaxAge = refreshTokenLifetime
            };
        }
    }
}
