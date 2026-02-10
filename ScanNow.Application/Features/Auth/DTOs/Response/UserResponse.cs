using System;
using System.Collections.Generic;
using System.Text;

namespace ScanNow.Application.Features.Auth.DTOs.Response
{
    public class UserResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = "user";
        public bool IsEmailVerified { get; set; }
        public bool IsActive { get; set; }
        public DateTime DateOfBirth { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
