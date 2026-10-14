using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.GetGiftPreview;

public record GetGiftPreviewQuery(Guid UserId) : IRequest<Result<IReadOnlyList<GiftPreviewDto>>>;
