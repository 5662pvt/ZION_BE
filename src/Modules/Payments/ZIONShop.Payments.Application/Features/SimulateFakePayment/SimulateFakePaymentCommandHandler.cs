using MediatR;
using Microsoft.Extensions.Hosting;
using ZIONShop.Payments.Application.DTOs;
using ZIONShop.Payments.Application.Interfaces;
using ZIONShop.Payments.Application.Services;
using ZIONShop.Payments.Domain.Exceptions;
using ZIONShop.Payments.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Payments.Application.Features.SimulateFakePayment;

public class SimulateFakePaymentCommandHandler : IRequestHandler<SimulateFakePaymentCommand, Result<PaymentIntentDto>>
{
    private readonly IPaymentRepository _payments;
    private readonly IPaymentGateway _gateway;
    private readonly FakeWebhookProcessor _processor;
    private readonly IHostEnvironment _environment;

    public SimulateFakePaymentCommandHandler(
        IPaymentRepository payments,
        IPaymentGateway gateway,
        FakeWebhookProcessor processor,
        IHostEnvironment environment)
    {
        _payments = payments;
        _gateway = gateway;
        _processor = processor;
        _environment = environment;
    }

    public async Task<Result<PaymentIntentDto>> Handle(SimulateFakePaymentCommand request, CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
            return Result.Failure<PaymentIntentDto>(PaymentsErrors.SimulationNotAvailable);

        string webhookResult;
        switch (request.Result?.Trim().ToLowerInvariant())
        {
            case "success": webhookResult = "succeeded"; break;
            case "failure": webhookResult = "failed"; break;
            default: return Result.Failure<PaymentIntentDto>(PaymentsErrors.InvalidSimulationResult);
        }

        var payment = await _payments.GetByIntentIdAsync(request.IntentId, cancellationToken);
        if (payment is null || payment.UserId != request.UserId)
            return Result.Failure<PaymentIntentDto>(PaymentsErrors.PaymentNotFound);

        var payload = new FakeWebhookPayload(
            Guid.NewGuid(),
            payment.IntentId,
            webhookResult,
            webhookResult == "failed" ? "Giả lập thanh toán thất bại" : null);

        var rawBody = System.Text.Json.JsonSerializer.Serialize(payload, FakeWebhookJson.Options);
        var signature = _gateway.SignPayload(rawBody);

        return await _processor.ProcessAsync(rawBody, signature, cancellationToken);
    }
}
