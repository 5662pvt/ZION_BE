using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.UpdateFlashSale;

public record UpdateFlashSaleCommand(Guid FlashSaleId, string Name, DateTime StartsAt, DateTime EndsAt) : IRequest<Result<FlashSaleDto>>;
