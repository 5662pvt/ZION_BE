using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.GetPromotionById;

public record GetPromotionByIdQuery(Guid PromotionId) : IRequest<Result<PromotionDto>>;
