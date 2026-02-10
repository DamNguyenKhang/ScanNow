using System;
using System.Collections.Generic;
using System.Text;

namespace ScanNow.Application.Features.Auth.DTOs.Response
{
    public class AuthResponse
    {
        public UserResponse? User { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }
}
