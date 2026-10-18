using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.GetActiveFlashSales;

public record GetActiveFlashSalesQuery : IRequest<Result<IReadOnlyList<ActiveFlashSaleDto>>>;
