using AutoMapper;
using ScanNow.Application.Features.Auth.DTOs.Response;
using ScanNow.Domain.Entities;

namespace ScanNow.Application.Mappers
{
    public class UserMapper : Profile
    {
        public UserMapper()
        {
            CreateMap<ApplicationUser, UserResponse>()
                .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.UserName))
                .ForMember(dest => dest.IsEmailVerified, opt => opt.MapFrom(src => src.EmailConfirmed))
                .ForMember(dest => dest.Role, opt => opt.Ignore());
        }
    }
}
