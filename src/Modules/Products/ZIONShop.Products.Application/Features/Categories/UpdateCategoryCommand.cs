using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.Categories;

public record UpdateCategoryCommand(Guid Id, string Name, string? Description, int DisplayOrder) : IRequest<Result<CategoryDto>>;
