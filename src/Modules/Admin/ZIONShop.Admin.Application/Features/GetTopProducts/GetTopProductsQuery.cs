using MediatR;
using ZIONShop.Admin.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Application.Features.GetTopProducts;

public record GetTopProductsQuery(string? From, string? To, int Limit, string? SortBy) : IRequest<Result<IReadOnlyList<TopProductDto>>>;
