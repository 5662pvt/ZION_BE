using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.Categories;

public record GetCategoriesQuery(bool AsTree = true) : IRequest<Result<IReadOnlyList<CategoryTreeNodeDto>>>;
