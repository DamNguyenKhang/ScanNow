using System.Text.Json.Serialization;

namespace ScanNow.Application.Features.Auth.DTOs.Response
{
    public class AuthResponse
    {
        public UserResponse? User { get; set; }
        public string AccessToken { get; set; } = null!;

        [JsonIgnore]
        public string? RefreshToken { get; set; }
    }
}
