using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.SetVariants;

public record SetVariantsCommand(
    Guid ProductId,
    IReadOnlyList<SetVariantsOptionInput> Options,
    IReadOnlyList<SetVariantsVariantInput> Variants) : IRequest<Result<ProductDto>>;

public record SetVariantsOptionInput(string Name, IReadOnlyList<string> Values);

public record SetVariantsVariantInput(
    Guid? Id,
    string? Option1Value,
    string? Option2Value,
    string Sku,
    decimal Price,
    string Currency,
    bool IsActive,
    Guid? ImageId);
