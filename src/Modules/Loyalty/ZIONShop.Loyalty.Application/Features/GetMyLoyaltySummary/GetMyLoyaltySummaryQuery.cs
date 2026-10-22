using MediatR;
using ZIONShop.Loyalty.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Loyalty.Application.Features.GetMyLoyaltySummary;

public record GetMyLoyaltySummaryQuery(Guid UserId) : IRequest<Result<LoyaltySummaryDto>>;
