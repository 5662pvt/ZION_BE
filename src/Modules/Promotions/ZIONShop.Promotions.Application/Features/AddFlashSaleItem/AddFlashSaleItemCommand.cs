using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.AddFlashSaleItem;

public record AddFlashSaleItemCommand(Guid FlashSaleId, Guid ProductId, decimal FlashPrice, int AllocatedQuantity) : IRequest<Result<FlashSaleItemDto>>;
