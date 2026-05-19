using System.ComponentModel.DataAnnotations;

namespace ScanNow.Application.Features.Auth.DTOs.Requests
{
    public class ChangePasswordRequest
    {
        [Required]
        public string CurrentPassword { get; set; } = null!;

        [Required]
        public string NewPassword { get; set; } = null!;
    }
}
