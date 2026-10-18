using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.DeactivateFlashSale;

public record DeactivateFlashSaleCommand(Guid FlashSaleId) : IRequest<Result<FlashSaleDto>>;
