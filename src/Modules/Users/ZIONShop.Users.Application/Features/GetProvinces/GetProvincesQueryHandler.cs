using AutoMapper;
using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;
using ZIONShop.Users.Domain.Repositories;

namespace ZIONShop.Users.Application.Features.GetProvinces;

public class GetProvincesQueryHandler : IRequestHandler<GetProvincesQuery, Result<IReadOnlyList<ProvinceDto>>>
{
    private readonly IAdministrativeUnitRepository _administrativeUnits;
    private readonly IMapper _mapper;

    public GetProvincesQueryHandler(IAdministrativeUnitRepository administrativeUnits, IMapper mapper)
    {
        _administrativeUnits = administrativeUnits;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<ProvinceDto>>> Handle(GetProvincesQuery request, CancellationToken cancellationToken)
    {
        var provinces = await _administrativeUnits.GetProvincesAsync(cancellationToken);
        IReadOnlyList<ProvinceDto> mapped = provinces.Select(p => _mapper.Map<ProvinceDto>(p)).ToList();
        return Result.Success(mapped);
    }
}
