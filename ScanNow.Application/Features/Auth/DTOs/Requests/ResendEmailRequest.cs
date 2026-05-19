using System.ComponentModel.DataAnnotations;

namespace ScanNow.Application.Features.Auth.DTOs.Requests
{
    public class ResendEmailRequest
    {
        [Required]
        public string Email { get; set; } = null!;
    }
}
