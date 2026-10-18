using MediatR;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.AnswerProductQuestion;

public record AnswerProductQuestionCommand(Guid QuestionId, Guid AnsweredByUserId, string Content) : IRequest<Result<ProductQuestionDto>>;
