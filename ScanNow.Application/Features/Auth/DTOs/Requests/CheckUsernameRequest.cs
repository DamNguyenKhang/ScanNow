using System.ComponentModel.DataAnnotations;

namespace ScanNow.Application.Features.Auth.DTOs.Requests
{
    public class CheckUsernameRequest
    {
        [Required]
        public string Username { get; set; } = null!;
    }
}
