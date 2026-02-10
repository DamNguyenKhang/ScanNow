using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Exceptions;
using ScanNow.Application.Features.Auth.DTOs.Requests;
using ScanNow.Application.Features.Auth.DTOs.Response;
using ScanNow.Domain.Abstractions.External;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Exceptions;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ScanNow.Application.Features.Auth
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IMapper _userMapper;
        private readonly IEmailService _emailService;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IConfiguration _configuration;

        public AuthService
            (
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IMapper userMapper,
            IEmailService emailService,
            IRefreshTokenRepository refreshTokenRepository,
            IConfiguration configuration
            )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _userMapper = userMapper;
            _emailService = emailService;
            _refreshTokenRepository = refreshTokenRepository;
            _configuration = configuration;
        }

        public async Task<AuthResponse> LoginAsync(AuthRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email) ?? throw new UnauthorizedException();
            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                throw new UnauthorizedException();
            }

            if (!user.EmailConfirmed)
            {
                await SendEmailVerification(user);
                throw new BusinessRuleException("Email not verify");
            }

            return new AuthResponse
            {
                User = _userMapper.Map<UserResponse>(user),
                AccessToken = await GenerateTokenAsync(user),
                RefreshToken = await GenerateAndSaveRefreshToken(user)
            };
        }

        public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId) ?? throw new UnauthorizedException();
            if (user is null || !await ValidateRefreshTokenAsync(request.RefreshToken))
            {
                throw new UnauthorizedException();
            }
            return new AuthResponse
            {
                AccessToken = await GenerateTokenAsync(user),
                RefreshToken = await GenerateAndSaveRefreshToken(user)
            };
        }

        public async Task<AuthResponse> VerifyEmailAsync(VerifyEmailRequest request)
        {
            var user = await _userManager.FindByIdAsync(request?.UserId);
            if (user is null)
            {
                throw new NotFoundException("User Not Found");
            }
            var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));

            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
            if (!result.Succeeded)
            {
                var errorMessage = string.Join(
                "; ",
                result.Errors.Select(e => e.Description)
            );

                throw new DomainException(errorMessage);
            }
            return new AuthResponse
            {
                User = _userMapper.Map<UserResponse>(user),
                AccessToken = await GenerateTokenAsync(user),
                RefreshToken = await GenerateAndSaveRefreshToken(user)
            };
        }

        private async Task<bool> ValidateRefreshTokenAsync(string refreshToken)
        {
            var refreshTokenEntity = await _refreshTokenRepository.GetByTokenAsync(refreshToken);
            return refreshTokenEntity is not null && refreshTokenEntity.ExpiresAt > DateTime.UtcNow && !refreshTokenEntity.IsRevoked;
        }

        private async Task SendEmailVerification(ApplicationUser user)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            var encodedToken = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(token)
            );

            var verifyUrl =
                $"{_configuration["App:ClientUrl"]!}/verify-email" +
                $"?userId={user.Id}&token={encodedToken}";

            var body = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:auto">
                <h2>🎉 Chào mừng bạn đến với ScanNow</h2>
                <p>Cảm ơn bạn đã đăng ký tài khoản.</p>
                <p>Vui lòng nhấn nút bên dưới để xác thực email:</p>

                <p style="text-align:center;margin:30px 0">
                    <a href="{verifyUrl}"
                       style="background:#4f46e5;color:#fff;
                              padding:12px 24px;
                              text-decoration:none;
                              border-radius:6px;
                              display:inline-block">
                       Xác thực email
                    </a>
                </p>

                <p>Link này sẽ hết hạn sau <b>15 phút</b>.</p>
                <p>Nếu bạn không đăng ký, vui lòng bỏ qua email này.</p>

                <hr />
                <p style="font-size:12px;color:#666">
                    © {DateTime.UtcNow.Year} ScanNow
                </p>
            </div>
            """;

            await _emailService.SendAsync(
                user.Email!,
                "Verify your email",
                body
            );

        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private async Task<string> GenerateAndSaveRefreshToken(ApplicationUser user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var refreshToken = new RefreshToken
            {
                Token = GenerateRefreshToken(),
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(double.Parse(jwtSettings["refreshTokenExpirationDays"]!))
            };
            await _refreshTokenRepository.AddAsync(refreshToken);
            return refreshToken.Token;
        }

        private async Task<string> GenerateTokenAsync(ApplicationUser user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");

            var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName ?? "")
        };

            // Roles
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["Key"]!));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(int.Parse(jwtSettings["AccessTokenExpirationMinutes"]!)),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);

        }
    }
}
