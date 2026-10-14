using MediatR;
using ZIONShop.Payments.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Payments.Application.Features.CreatePaymentIntent;

public record CreatePaymentIntentCommand(Guid OrderId, Guid UserId, string IdempotencyKey) : IRequest<Result<PaymentIntentDto>>;
