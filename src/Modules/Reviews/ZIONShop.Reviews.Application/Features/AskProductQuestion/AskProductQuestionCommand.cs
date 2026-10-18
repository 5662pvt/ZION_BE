using MediatR;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.AskProductQuestion;

public record AskProductQuestionCommand(Guid UserId, Guid ProductId, string Content) : IRequest<Result<ProductQuestionDto>>;
