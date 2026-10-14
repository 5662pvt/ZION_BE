using MediatR;
using ZIONShop.Payments.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Payments.Application.Features.HandleFakeWebhook;

public record HandleFakeWebhookCommand(string RawBody, string Signature) : IRequest<Result<PaymentIntentDto>>;
