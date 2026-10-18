using MediatR;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.RemoveReviewReply;

public record RemoveReviewReplyCommand(Guid ReviewId) : IRequest<Result<AdminReviewDto>>;
