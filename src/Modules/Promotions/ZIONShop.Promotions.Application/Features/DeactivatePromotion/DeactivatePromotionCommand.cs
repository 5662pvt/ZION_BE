using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.DeactivatePromotion;

public record DeactivatePromotionCommand(Guid PromotionId) : IRequest<Result<PromotionDto>>;
