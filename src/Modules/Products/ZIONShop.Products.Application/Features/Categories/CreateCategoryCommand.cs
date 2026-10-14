using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.Categories;

public record CreateCategoryCommand(string Name, string Slug, Guid? ParentId, string? Description, int DisplayOrder) : IRequest<Result<CategoryDto>>;
