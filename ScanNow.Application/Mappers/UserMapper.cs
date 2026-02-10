using AutoMapper;
using ScanNow.Application.Features.Auth.DTOs.Response;
using ScanNow.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScanNow.Application.Mappers
{
    public class UserMapper : Profile
    {
        public UserMapper()
        {
            //CreateMap<SignUpUserRequest, User>()
            //.ForMember(dest => dest.PasswordHash, opt => opt.MapFrom(src => src.Password));

            CreateMap<ApplicationUser, UserResponse>();
        }
    }
}
