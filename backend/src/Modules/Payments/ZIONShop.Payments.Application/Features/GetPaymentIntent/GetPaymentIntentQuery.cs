using MediatR;
using ZIONShop.Payments.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Payments.Application.Features.GetPaymentIntent;

public record GetPaymentIntentQuery(string IntentId, Guid UserId) : IRequest<Result<PaymentIntentDto>>;
