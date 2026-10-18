using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;

namespace ZIONShop.Users.Application.Features.GetProvinces;

public record GetProvincesQuery : IRequest<Result<IReadOnlyList<ProvinceDto>>>;
