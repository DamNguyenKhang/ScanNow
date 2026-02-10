namespace ScanNow.Application.Features.Auth.DTOs.Requests
{
    public class AuthRequest
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }
}
