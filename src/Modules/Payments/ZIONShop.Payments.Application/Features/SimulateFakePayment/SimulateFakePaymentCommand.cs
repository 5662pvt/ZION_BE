using MediatR;
using ZIONShop.Payments.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Payments.Application.Features.SimulateFakePayment;

public record SimulateFakePaymentCommand(string IntentId, Guid UserId, string Result) : IRequest<Result<PaymentIntentDto>>;
