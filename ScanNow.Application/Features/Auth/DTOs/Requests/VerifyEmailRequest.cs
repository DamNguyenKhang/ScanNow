using System.ComponentModel.DataAnnotations;

namespace ScanNow.Application.Features.Auth.DTOs.Requests
{
    public class VerifyEmailRequest
    {
        [Required]
        public string UserId { get; set; }
        [Required]
        public string Token { get; set; }
    }
}
