using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.GetFlashSaleById;

public record GetFlashSaleByIdQuery(Guid FlashSaleId) : IRequest<Result<FlashSaleDto>>;
