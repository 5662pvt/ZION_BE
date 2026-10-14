using MediatR;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.ReorderProductImages;

public record ReorderProductImagesCommand(Guid ProductId, IReadOnlyList<Guid> ImageIds) : IRequest<Result>;
