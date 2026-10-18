using AutoMapper;
using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;
using ZIONShop.Users.Domain.Exceptions;
using ZIONShop.Users.Domain.Repositories;

namespace ZIONShop.Users.Application.Features.GetWardsByProvince;

public class GetWardsByProvinceQueryHandler : IRequestHandler<GetWardsByProvinceQuery, Result<IReadOnlyList<WardDto>>>
{
    private readonly IAdministrativeUnitRepository _administrativeUnits;
    private readonly IMapper _mapper;

    public GetWardsByProvinceQueryHandler(IAdministrativeUnitRepository administrativeUnits, IMapper mapper)
    {
        _administrativeUnits = administrativeUnits;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<WardDto>>> Handle(GetWardsByProvinceQuery request, CancellationToken cancellationToken)
    {
        var province = await _administrativeUnits.GetProvinceByIdAsync(request.ProvinceId, cancellationToken);
        if (province is null) return Result.Failure<IReadOnlyList<WardDto>>(UsersErrors.ProvinceNotFound);

        var wards = await _administrativeUnits.GetWardsByProvinceIdAsync(request.ProvinceId, cancellationToken);
        IReadOnlyList<WardDto> mapped = wards.Select(w => _mapper.Map<WardDto>(w)).ToList();
        return Result.Success(mapped);
    }
}
