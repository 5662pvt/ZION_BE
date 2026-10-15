using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Common.Api;
using ZIONShop.Payments.Application.Features.CreatePaymentIntent;
using ZIONShop.Payments.Application.Features.GetPaymentIntent;
using ZIONShop.Payments.Application.Features.HandleFakeWebhook;
using ZIONShop.Payments.Application.Features.SimulateFakePayment;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public PaymentsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpPost("intents")]
    [Authorize]
    public async Task<IActionResult> CreateIntent(
        [FromBody] CreatePaymentIntentBody body,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(ApiResponse.Fail("Idempotency-Key header is required"));

        var cmd = new CreatePaymentIntentCommand(body.OrderId, _currentUser.UserId!.Value, idempotencyKey, body.Channel);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Payment intent created");
    }

    [HttpGet("intents/{intentId}")]
    [Authorize]
    public async Task<IActionResult> GetIntent(string intentId, CancellationToken ct)
        => (await _mediator.Send(new GetPaymentIntentQuery(intentId, _currentUser.UserId!.Value), ct)).ToActionResult();

    [HttpPost("fake/{intentId}/simulate")]
    [Authorize]
    public async Task<IActionResult> SimulateFake(string intentId, [FromBody] SimulateFakePaymentBody body, CancellationToken ct)
        => (await _mediator.Send(new SimulateFakePaymentCommand(intentId, _currentUser.UserId!.Value, body.Result), ct)).ToActionResult("Simulated");

    [HttpPost("webhooks/fake")]
    [AllowAnonymous]
    public async Task<IActionResult> FakeWebhook()
    {
        string rawBody;
        Request.EnableBuffering();
        Request.Body.Position = 0;
        using (var reader = new StreamReader(Request.Body, leaveOpen: true))
            rawBody = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        var signature = Request.Headers["X-Fake-Signature"].ToString();
        var result = await _mediator.Send(new HandleFakeWebhookCommand(rawBody, signature));

        if (result.IsFailure && result.Error.Type == ErrorType.Unauthorized)
            return Unauthorized(ApiResponse.Fail(result.Error.Message));

        // Webhook contract is only ever 200/401: ack everything else (unknown intent, already-processed
        // event, order cancelled meanwhile, ...) so the provider doesn't keep retrying a delivery that
        // will never succeed.
        return Ok(ApiResponse.Ok("Webhook processed"));
    }
}

public record CreatePaymentIntentBody(Guid OrderId, string? Channel = null);
public record SimulateFakePaymentBody(string Result);
