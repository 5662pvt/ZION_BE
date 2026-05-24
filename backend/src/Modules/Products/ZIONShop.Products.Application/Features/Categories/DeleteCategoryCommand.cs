using MediatR;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.Categories;

public record DeleteCategoryCommand(Guid Id) : IRequest<Result>;
