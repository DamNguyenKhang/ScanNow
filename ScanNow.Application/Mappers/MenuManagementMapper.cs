using AutoMapper;
using ScanNow.Application.Features.MenuManagement.DTOs;
using ScanNow.Domain.Entities;

namespace ScanNow.Application.Mappers
{
    public class MenuManagementMapper : Profile
    {
        public MenuManagementMapper()
        {
            CreateMap<Category, CategoryResponse>()
                .ForMember(dest => dest.CategoryId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.BranchName, opt => opt.MapFrom(src => src.Branch.Name));

            CreateMap<MenuItem, MenuItemResponse>()
                .ForMember(dest => dest.MenuItemId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.BranchName, opt => opt.MapFrom(src => src.Branch.Name))
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name));

            CreateMap<MenuItemPriceHistory, PriceHistoryResponse>()
                .ForMember(dest => dest.PriceHistoryId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.ChangedByName, opt => opt.MapFrom(src => src.ChangedBy.FullName));
        }
    }
}
