using MediatR;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.Reviews.Application.Interfaces;
using ZIONShop.Reviews.Application.Mappings;
using ZIONShop.Reviews.Domain.Exceptions;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.AnswerProductQuestion;

public class AnswerProductQuestionCommandHandler : IRequestHandler<AnswerProductQuestionCommand, Result<ProductQuestionDto>>
{
    private readonly IProductQuestionRepository _questions;
    private readonly IReviewsUnitOfWork _uow;

    public AnswerProductQuestionCommandHandler(IProductQuestionRepository questions, IReviewsUnitOfWork uow)
    {
        _questions = questions;
        _uow = uow;
    }

    public async Task<Result<ProductQuestionDto>> Handle(AnswerProductQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = await _questions.GetByIdAsync(request.QuestionId, cancellationToken);
        if (question is null) return Result.Failure<ProductQuestionDto>(ReviewsErrors.QuestionNotFound);

        var result = question.Answer(request.AnsweredByUserId, request.Content);
        if (result.IsFailure) return Result.Failure<ProductQuestionDto>(result.Error);

        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(question.ToDto());
    }
}
