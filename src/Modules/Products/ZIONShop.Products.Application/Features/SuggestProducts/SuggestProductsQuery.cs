using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.SuggestProducts;

public record SuggestProductsQuery(string? Q, int? Limit) : IRequest<Result<IReadOnlyList<ProductSuggestionDto>>>;
