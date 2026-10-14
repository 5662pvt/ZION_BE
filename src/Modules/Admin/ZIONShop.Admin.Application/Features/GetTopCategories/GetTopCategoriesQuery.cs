using MediatR;
using ZIONShop.Admin.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Application.Features.GetTopCategories;

public record GetTopCategoriesQuery(string? From, string? To, string? SortBy) : IRequest<Result<IReadOnlyList<TopCategoryDto>>>;
