using MediatR;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.DeleteProductQuestion;

public record DeleteProductQuestionCommand(Guid QuestionId, Guid UserId, bool IsStaffOrAdmin) : IRequest<Result>;
