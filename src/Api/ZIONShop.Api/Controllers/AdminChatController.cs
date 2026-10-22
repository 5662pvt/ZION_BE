using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ZIONShop.Auth.Authorization;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Chat.Application.Features.GetConversationById;
using ZIONShop.Chat.Application.Features.GetConversationMessages;
using ZIONShop.Chat.Application.Features.GetConversations;
using ZIONShop.Chat.Application.Features.GetStaffUnreadCount;
using ZIONShop.Chat.Application.Features.MarkConversationReadByStaff;
using ZIONShop.Chat.Application.Features.SendStaffImage;
using ZIONShop.Chat.Application.Features.SendStaffMessage;
using ZIONShop.Common.Api;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/admin/chat")]
[Authorize(Policy = Policies.StaffOrAdmin)]
public class AdminChatController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public AdminChatController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations([FromQuery] AdminChatConversationsRequest req, CancellationToken ct)
        => (await _mediator.Send(new GetConversationsQuery(req.UnreadOnly, req.Search, req.Page, req.PageSize), ct)).ToPagedActionResult();

    [HttpGet("conversations/{id:guid}")]
    public async Task<IActionResult> GetConversation(Guid id, CancellationToken ct)
        => (await _mediator.Send(new GetConversationByIdQuery(id), ct)).ToActionResult();

    [HttpGet("conversations/{id:guid}/messages")]
    public async Task<IActionResult> GetConversationMessages(Guid id, [FromQuery] ChatMessagesRequest req, CancellationToken ct)
        => (await _mediator.Send(new GetConversationMessagesQuery(id, req.Before, req.BeforeId, req.Limit), ct)).ToActionResult();

    [HttpPost("conversations/{id:guid}/messages")]
    [EnableRateLimiting("chat")]
    public async Task<IActionResult> SendMessage(Guid id, [FromBody] SendChatMessageBody body, CancellationToken ct)
    {
        var cmd = new SendStaffMessageCommand(_currentUser.UserId!.Value, id, body.Type, body.Text, body.ProductId, body.OrderId);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Đã gửi tin nhắn");
    }

    [HttpPost("conversations/{id:guid}/images")]
    [EnableRateLimiting("chat")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5_242_880 + 64_000)]
    public async Task<IActionResult> SendImage(Guid id, IFormFile file, [FromForm] string? caption, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var cmd = new SendStaffImageCommand(_currentUser.UserId!.Value, id, stream, file.FileName, file.ContentType, file.Length, caption);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Đã gửi hình ảnh");
    }

    [HttpPost("conversations/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
        => (await _mediator.Send(new MarkConversationReadByStaffCommand(id), ct)).ToActionResult();

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
        => (await _mediator.Send(new GetStaffUnreadCountQuery(), ct)).ToActionResult();
}

public record AdminChatConversationsRequest(bool UnreadOnly = false, string? Search = null, int Page = 1, int PageSize = 20);
