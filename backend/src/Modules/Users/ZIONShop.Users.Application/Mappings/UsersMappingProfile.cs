using AutoMapper;
using ZIONShop.Users.Application.DTOs;
using ZIONShop.Users.Domain.Entities;

namespace ZIONShop.Users.Application.Mappings;

public class UsersMappingProfile : Profile
{
    public UsersMappingProfile()
    {
        CreateMap<Address, AddressDto>();
        CreateMap<UserProfile, UserProfileDto>()
            .ForCtorParam(nameof(UserProfileDto.Addresses), opt => opt.MapFrom(src => src.Addresses));
    }
}
