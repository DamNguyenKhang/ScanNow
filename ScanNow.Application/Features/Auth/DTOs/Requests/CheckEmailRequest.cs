using System.ComponentModel.DataAnnotations;

namespace ScanNow.Application.Features.Auth.DTOs.Requests
{
    public class CheckEmailRequest
    {
        [Required]
        public string Email { get; set; } = null!;
    }
}
