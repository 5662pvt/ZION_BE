using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.Chat.Application.Interfaces;
using ZIONShop.Chat.Application.Mappings;
using ZIONShop.Chat.Application.Services;
using ZIONShop.Chat.Domain.Enums;
using ZIONShop.Realtime;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.MarkMyConversationRead;

public class MarkMyConversationReadCommandHandler : IRequestHandler<MarkMyConversationReadCommand, Result<ConversationDto>>
{
    private const int MaxSaveAttempts = 3;

    private readonly ConversationProvisioningService _provisioning;
    private readonly IChatUnitOfWork _uow;
    private readonly IRealtimeEventPublisher _realtime;
    private readonly TimeProvider _time;

    public MarkMyConversationReadCommandHandler(ConversationProvisioningService provisioning, IChatUnitOfWork uow, IRealtimeEventPublisher realtime, TimeProvider time)
    {
        _provisioning = provisioning;
        _uow = uow;
        _realtime = realtime;
        _time = time;
    }

    public async Task<Result<ConversationDto>> Handle(MarkMyConversationReadCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _provisioning.GetOrCreateAsync(request.UserId, cancellationToken);
        var now = _time.GetUtcNow().UtcDateTime;

        for (var attempt = 1; attempt <= MaxSaveAttempts; attempt++)
        {
            conversation.MarkReadBy(ChatSenderRole.Customer, now);
            try
            {
                await _uow.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxSaveAttempts)
            {
                _uow.Detach(conversation);
                conversation = await _provisioning.GetOrCreateAsync(request.UserId, cancellationToken);
            }
        }

        var dto = conversation.ToDto();

        try
        {
            await _realtime.PublishToStaffAsync("chat:read", new { conversationId = conversation.Id, side = "Customer", readAt = conversation.CustomerLastReadAt }, cancellationToken);
        }
        catch
        {
            // fail-open: realtime push must never fail the triggering business transaction
        }

        return Result.Success(dto);
    }
}
