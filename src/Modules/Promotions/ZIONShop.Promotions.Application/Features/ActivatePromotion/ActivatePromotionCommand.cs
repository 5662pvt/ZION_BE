using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.ActivatePromotion;

public record ActivatePromotionCommand(Guid PromotionId) : IRequest<Result<PromotionDto>>;
