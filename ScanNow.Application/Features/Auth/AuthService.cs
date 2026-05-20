using Google.Apis.Auth;
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
using ScanNow.Domain.Enums;
using ScanNow.Domain.Exceptions;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ScanNow.Application.Features.Auth
{
    public class AuthService : IAuthService
    {
        private static readonly string DefaultRole = UserRole.OWNER.ToString();
        private const string LocalProvider = "Local";
        private const string GoogleProvider = "Google";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ICurrentUserService _currentUserService;
        private readonly IEmailService _emailService;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<ApplicationRole> roleManager,
            ICurrentUserService currentUserService,
            IEmailService emailService,
            IRefreshTokenRepository refreshTokenRepository,
            IUnitOfWork unitOfWork,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _currentUserService = currentUserService;
            _emailService = emailService;
            _refreshTokenRepository = refreshTokenRepository;
            _unitOfWork = unitOfWork;
            _configuration = configuration;
        }

        public async Task<UserResponse> RegisterAsync(SignUpUserRequest request)
        {
            if (await _userManager.FindByEmailAsync(request.Email) is not null)
            {
                throw new ConflictException("Email already exists");
            }

            if (await _userManager.FindByNameAsync(request.Username) is not null)
            {
                throw new ConflictException("Username already exists");
            }

            var user = new ApplicationUser
            {
                Email = request.Email,
                UserName = request.Username,
                FullName = request.FullName,
                AuthProvider = LocalProvider,
                EmailConfirmed = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            EnsureSucceeded(result);

            await EnsureUserRoleAsync(user);
            await SendEmailVerificationAsync(user);

            return await MapUserResponseAsync(user);
        }

        public async Task<AuthResponse> LoginAsync(AuthRequest request)
        {
            var user = await FindByEmailOrUsernameAsync(request.Identifier) ?? throw new UnauthorizedException("Invalid credentials");
            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

            if (!result.Succeeded)
            {
                throw new UnauthorizedException("Invalid credentials");
            }

            if (!user.EmailConfirmed)
            {
                await SendEmailVerificationAsync(user);
                throw new BusinessRuleException("Email hasn't been verified");
            }

            await RecordLoginAsync(user);
            return await CreateAuthResponseAsync(user, includeRefreshToken: true);
        }

        public async Task<AuthResponse> LoginGoogleAsync(GoogleLoginRequest request)
        {
            var payload = await VerifyGoogleTokenAsync(request.IdToken);
            var user = await _userManager.FindByEmailAsync(payload.Email);

            if (user is null)
            {
                user = new ApplicationUser
                {
                    Email = payload.Email,
                    UserName = await GenerateUniqueUsernameAsync(payload.Email.Split('@')[0]),
                    FullName = payload.Name ?? payload.Email.Split('@')[0],
                    AvatarUrl = payload.Picture,
                    AuthProvider = GoogleProvider,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var createResult = await _userManager.CreateAsync(user);
                EnsureSucceeded(createResult);
                await EnsureUserRoleAsync(user);
            }
            else if (string.Equals(user.AuthProvider, LocalProvider, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedException("Invalid credentials");
            }

            await RecordLoginAsync(user);
            return await CreateAuthResponseAsync(user, includeRefreshToken: true);
        }

        public async Task<AuthResponse> RefreshTokenAsync(string refreshToken)
        {
            var refreshTokenEntity = await _refreshTokenRepository.GetByTokenAsync(refreshToken);
            if (refreshTokenEntity is null || refreshTokenEntity.ExpiresAt <= DateTime.UtcNow || refreshTokenEntity.IsRevoked)
            {
                throw new ForbiddenException("Invalid refresh token");
            }

            var user = await _userManager.FindByIdAsync(refreshTokenEntity.UserId.ToString()) ?? throw new UnauthorizedException();
            return await CreateAuthResponseAsync(user, includeRefreshToken: false);
        }

        public async Task LogoutAsync(string refreshToken)
        {
            var refreshTokenEntity = await _refreshTokenRepository.GetByTokenAsync(refreshToken) ?? throw new ForbiddenException("Invalid refresh token");
            refreshTokenEntity.IsRevoked = true;
            refreshTokenEntity.RevokedAt = DateTime.UtcNow;
            await _refreshTokenRepository.UpdateAsync(refreshTokenEntity);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<bool> CheckEmailExistsAsync(CheckEmailRequest request)
        {
            return await _userManager.FindByEmailAsync(request.Email) is not null;
        }

        public async Task<bool> CheckUsernameExistsAsync(CheckUsernameRequest request)
        {
            return await _userManager.FindByNameAsync(request.Username) is not null;
        }

        public async Task<bool> ChangePasswordAsync(ChangePasswordRequest request)
        {
            var userId = _currentUserService.UserId ?? throw new UnauthorizedException();
            var user = await _userManager.FindByIdAsync(userId.ToString()) ?? throw new UnauthorizedException();

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            EnsureSucceeded(result);

            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            return true;
        }

        public async Task<AuthResponse> VerifyEmailAsync(VerifyEmailRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId) ?? throw new NotFoundException("User not found");
            var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
            EnsureSucceeded(result);

            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            return await CreateAuthResponseAsync(user, includeRefreshToken: true);
        }

        public async Task ResendEmailVerificationAsync(ResendEmailRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email) ?? throw new NotFoundException("User not found");

            if (user.EmailConfirmed)
            {
                throw new BusinessRuleException("Email has already been verified");
            }

            await SendEmailVerificationAsync(user);
        }

        private async Task<ApplicationUser?> FindByEmailOrUsernameAsync(string identifier)
        {
            return identifier.Contains('@')
                ? await _userManager.FindByEmailAsync(identifier)
                : await _userManager.FindByNameAsync(identifier);
        }

        private async Task<GoogleJsonWebSignature.Payload> VerifyGoogleTokenAsync(string idToken)
        {
            try
            {
                var clientId = _configuration["Authentication:Google:ClientId"];
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    throw new DomainException("Google client id is not configured", HttpStatusCode.InternalServerError);
                }

                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                };

                return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            }
            catch (InvalidJwtException ex)
            {
                throw new ForbiddenException("Invalid Google token", ex);
            }
        }

        private async Task<AuthResponse> CreateAuthResponseAsync(ApplicationUser user, bool includeRefreshToken)
        {
            return new AuthResponse
            {
                User = await MapUserResponseAsync(user),
                AccessToken = await GenerateTokenAsync(user),
                RefreshToken = includeRefreshToken ? await GenerateAndSaveRefreshTokenAsync(user) : null
            };
        }

        private async Task<UserResponse> MapUserResponseAsync(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            return new UserResponse
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                Username = user.UserName ?? string.Empty,
                AvatarUrl = user.AvatarUrl,
                Role = roles.FirstOrDefault() ?? DefaultRole,
                IsEmailVerified = user.EmailConfirmed,
                IsActive = user.IsActive,
                FullName = user.FullName,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }

        private async Task EnsureUserRoleAsync(ApplicationUser user)
        {
            foreach (var role in Enum.GetValues<UserRole>().Select(role => role.ToString()))
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    EnsureSucceeded(await _roleManager.CreateAsync(new ApplicationRole(role)));
                }
            }

            if (!await _userManager.IsInRoleAsync(user, DefaultRole))
            {
                EnsureSucceeded(await _userManager.AddToRoleAsync(user, DefaultRole));
            }
        }

        private async Task SendEmailVerificationAsync(ApplicationUser user)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var frontendBaseUrl = _configuration["App:FrontendBaseUrl"] ?? _configuration["App:ClientUrl"] ?? "http://localhost:5173";
            var verifyPath = _configuration["App:VerifyEmailPath"] ?? "/verify-email";
            var verifyUrl = $"{frontendBaseUrl.TrimEnd('/')}/{verifyPath.TrimStart('/')}?userId={user.Id}&token={encodedToken}";

            var body = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:auto">
                <h2>Welcome to ScanNow</h2>
                <p>Thank you for registering your account.</p>
                <p>Please click the button below to verify your email:</p>

                <p style="text-align:center;margin:30px 0">
                    <a href="{verifyUrl}"
                       style="background:#4f46e5;color:#fff;
                              padding:12px 24px;
                              text-decoration:none;
                              border-radius:6px;
                              display:inline-block">
                       Verify email
                    </a>
                </p>

                <p>This link expires in <b>15 minutes</b>.</p>
                <p>If you did not register, please ignore this email.</p>

                <hr />
                <p style="font-size:12px;color:#666">
                    © {DateTime.UtcNow.Year} ScanNow
                </p>
            </div>
            """;

            await _emailService.SendAsync(user.Email!, "Verify your email", body);
        }

        private async Task RecordLoginAsync(ApplicationUser user)
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }

        private static string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private async Task<string> GenerateAndSaveRefreshTokenAsync(ApplicationUser user)
        {
            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = GenerateRefreshToken(),
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_configuration.GetValue<double>("Jwt:RefreshTokenExpirationDays"))
            };

            await _refreshTokenRepository.AddAsync(refreshToken);
            await _unitOfWork.SaveChangesAsync();
            return refreshToken.Token;
        }

        private async Task<string> GenerateTokenAsync(ApplicationUser user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var roles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email ?? string.Empty),
                new("name", user.UserName ?? string.Empty)
            };

            claims.AddRange(roles.Select(role => new Claim("role", role)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(jwtSettings.GetValue<int>("AccessTokenExpirationMinutes")),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<string> GenerateUniqueUsernameAsync(string username)
        {
            var normalized = new string(username.Where(char.IsLetterOrDigit).ToArray());
            var baseUsername = string.IsNullOrWhiteSpace(normalized) ? "google_user" : normalized[..Math.Min(normalized.Length, 20)];
            var candidate = baseUsername;
            var suffix = 1;

            while (await _userManager.FindByNameAsync(candidate) is not null)
            {
                var suffixText = suffix++.ToString();
                candidate = $"{baseUsername[..Math.Min(baseUsername.Length, 20 - suffixText.Length)]}{suffixText}";
            }

            return candidate;
        }

        private static void EnsureSucceeded(IdentityResult result)
        {
            if (result.Succeeded)
            {
                return;
            }

            var errors = result.Errors
                .GroupBy(error => error.Code)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.Description).ToArray());

            throw new ScanNow.Domain.Exceptions.ValidationException(errors);
        }
    }
}
