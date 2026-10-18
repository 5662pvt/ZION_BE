using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.ActivateFlashSale;

public record ActivateFlashSaleCommand(Guid FlashSaleId) : IRequest<Result<FlashSaleDto>>;
