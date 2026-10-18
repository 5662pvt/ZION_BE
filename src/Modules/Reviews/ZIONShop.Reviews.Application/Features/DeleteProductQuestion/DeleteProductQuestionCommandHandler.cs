using MediatR;
using ZIONShop.Reviews.Application.Interfaces;
using ZIONShop.Reviews.Domain.Exceptions;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.DeleteProductQuestion;

public class DeleteProductQuestionCommandHandler : IRequestHandler<DeleteProductQuestionCommand, Result>
{
    private readonly IProductQuestionRepository _questions;
    private readonly IReviewsUnitOfWork _uow;

    public DeleteProductQuestionCommandHandler(IProductQuestionRepository questions, IReviewsUnitOfWork uow)
    {
        _questions = questions;
        _uow = uow;
    }

    public async Task<Result> Handle(DeleteProductQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = await _questions.GetByIdAsync(request.QuestionId, cancellationToken);
        if (question is null) return Result.Failure(ReviewsErrors.QuestionNotFound);

        if (question.UserId != request.UserId && !request.IsStaffOrAdmin)
            return Result.Failure(ReviewsErrors.NotQuestionOwner);

        _questions.Remove(question);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
