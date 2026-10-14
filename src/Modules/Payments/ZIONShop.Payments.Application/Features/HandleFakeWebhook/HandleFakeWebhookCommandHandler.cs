using MediatR;
using ZIONShop.Payments.Application.DTOs;
using ZIONShop.Payments.Application.Services;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Payments.Application.Features.HandleFakeWebhook;

public class HandleFakeWebhookCommandHandler : IRequestHandler<HandleFakeWebhookCommand, Result<PaymentIntentDto>>
{
    private readonly FakeWebhookProcessor _processor;

    public HandleFakeWebhookCommandHandler(FakeWebhookProcessor processor) => _processor = processor;

    public async Task<Result<PaymentIntentDto>> Handle(HandleFakeWebhookCommand request, CancellationToken cancellationToken) =>
        await _processor.ProcessAsync(request.RawBody, request.Signature, cancellationToken);
}
