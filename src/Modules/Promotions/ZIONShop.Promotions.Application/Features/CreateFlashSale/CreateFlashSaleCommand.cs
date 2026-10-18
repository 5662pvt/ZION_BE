using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.CreateFlashSale;

public record CreateFlashSaleCommand(string Name, DateTime StartsAt, DateTime EndsAt) : IRequest<Result<FlashSaleDto>>;
