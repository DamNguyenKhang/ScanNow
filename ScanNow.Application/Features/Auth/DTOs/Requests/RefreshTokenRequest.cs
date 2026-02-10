using System;
using System.Collections.Generic;
using System.Text;

namespace ScanNow.Application.Features.Auth.DTOs.Requests
{
    public class RefreshTokenRequest
    {
        public required string UserId { get; set; }
        public required string RefreshToken { get; set; }
    }
}
