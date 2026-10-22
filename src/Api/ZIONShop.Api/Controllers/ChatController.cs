using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Chat.Application.Features.GetMyConversation;
using ZIONShop.Chat.Application.Features.GetMyMessages;
using ZIONShop.Chat.Application.Features.MarkMyConversationRead;
using ZIONShop.Chat.Application.Features.SendMyImage;
using ZIONShop.Chat.Application.Features.SendMyMessage;
using ZIONShop.Common.Api;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ChatController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
        => (await _mediator.Send(new GetMyConversationQuery(_currentUser.UserId!.Value), ct)).ToActionResult();

    [HttpGet("me/messages")]
    public async Task<IActionResult> GetMyMessages([FromQuery] ChatMessagesRequest req, CancellationToken ct)
        => (await _mediator.Send(new GetMyMessagesQuery(_currentUser.UserId!.Value, req.Before, req.Limit), ct)).ToActionResult();

    [HttpPost("me/messages")]
    [EnableRateLimiting("chat")]
    public async Task<IActionResult> SendMyMessage([FromBody] SendChatMessageBody body, CancellationToken ct)
    {
        var cmd = new SendMyMessageCommand(_currentUser.UserId!.Value, body.Type, body.Text, body.ProductId, body.OrderId);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Đã gửi tin nhắn");
    }

    [HttpPost("me/images")]
    [EnableRateLimiting("chat")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5_242_880 + 64_000)]
    public async Task<IActionResult> SendMyImage(IFormFile file, [FromForm] string? caption, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var cmd = new SendMyImageCommand(_currentUser.UserId!.Value, stream, file.FileName, file.ContentType, file.Length, caption);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Đã gửi hình ảnh");
    }

    [HttpPost("me/read")]
    public async Task<IActionResult> MarkMyConversationRead(CancellationToken ct)
        => (await _mediator.Send(new MarkMyConversationReadCommand(_currentUser.UserId!.Value), ct)).ToActionResult();
}

public record ChatMessagesRequest(DateTime? Before = null, int Limit = 30);
public record SendChatMessageBody(string Type, string? Text, Guid? ProductId, Guid? OrderId);
