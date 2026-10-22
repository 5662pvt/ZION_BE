using MediatR;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.Common.Pagination;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.GetConversations;

public record GetConversationsQuery(bool UnreadOnly, string? Search, int Page, int PageSize) : IRequest<Result<PagedResult<ConversationDto>>>;
