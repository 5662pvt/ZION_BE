using MediatR;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.GetStaffUnreadCount;

public record GetStaffUnreadCountQuery : IRequest<Result<StaffUnreadCountDto>>;
