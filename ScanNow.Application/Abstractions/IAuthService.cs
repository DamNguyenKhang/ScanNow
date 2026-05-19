using ScanNow.Application.Features.Auth.DTOs.Requests;
using ScanNow.Application.Features.Auth.DTOs.Response;

namespace ScanNow.Application.Abstractions
{
    public interface IAuthService
    {
        Task<UserResponse> RegisterAsync(SignUpUserRequest request);
        Task<AuthResponse> LoginAsync(AuthRequest request);
        Task<AuthResponse> LoginGoogleAsync(GoogleLoginRequest request);
        Task<AuthResponse> RefreshTokenAsync(string refreshToken);
        Task LogoutAsync(string refreshToken);
        Task<bool> CheckEmailExistsAsync(CheckEmailRequest request);
        Task<bool> CheckUsernameExistsAsync(CheckUsernameRequest request);
        Task<bool> ChangePasswordAsync(ChangePasswordRequest request);
        Task<AuthResponse> VerifyEmailAsync(VerifyEmailRequest request);
        Task ResendEmailVerificationAsync(ResendEmailRequest request);
    }
}
