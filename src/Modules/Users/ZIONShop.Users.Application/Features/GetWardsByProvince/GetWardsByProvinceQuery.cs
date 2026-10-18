using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;

namespace ZIONShop.Users.Application.Features.GetWardsByProvince;

public record GetWardsByProvinceQuery(Guid ProvinceId) : IRequest<Result<IReadOnlyList<WardDto>>>;
