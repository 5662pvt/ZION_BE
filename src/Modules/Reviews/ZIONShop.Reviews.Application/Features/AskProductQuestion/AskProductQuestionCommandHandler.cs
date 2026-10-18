using MediatR;
using ZIONShop.Contracts.Auth;
using ZIONShop.Contracts.Products;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.Reviews.Application.Interfaces;
using ZIONShop.Reviews.Application.Mappings;
using ZIONShop.Reviews.Domain.Entities;
using ZIONShop.Reviews.Domain.Exceptions;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.AskProductQuestion;

public class AskProductQuestionCommandHandler : IRequestHandler<AskProductQuestionCommand, Result<ProductQuestionDto>>
{
    private readonly IProductQuestionRepository _questions;
    private readonly IReviewsUnitOfWork _uow;
    private readonly IUserContactReader _userContactReader;
    private readonly IProductCatalogReader _catalogReader;

    public AskProductQuestionCommandHandler(
        IProductQuestionRepository questions,
        IReviewsUnitOfWork uow,
        IUserContactReader userContactReader,
        IProductCatalogReader catalogReader)
    {
        _questions = questions;
        _uow = uow;
        _userContactReader = userContactReader;
        _catalogReader = catalogReader;
    }

    public async Task<Result<ProductQuestionDto>> Handle(AskProductQuestionCommand request, CancellationToken cancellationToken)
    {
        var product = await _catalogReader.GetAsync(request.ProductId, cancellationToken);
        if (product is null || product.Status != "Published")
            return Result.Failure<ProductQuestionDto>(ReviewsErrors.ProductNotAvailable);

        var contact = await _userContactReader.GetAsync(request.UserId, cancellationToken);
        var authorName = MaskAuthorName(contact);

        var question = ProductQuestion.Create(request.ProductId, request.UserId, authorName, request.Content.Trim());

        await _questions.AddAsync(question, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(question.ToDto());
    }

    private static string MaskAuthorName(UserContact? contact)
    {
        if (contact is null) return "Người dùng ẩn danh";
        if (!string.IsNullOrWhiteSpace(contact.DisplayName)) return contact.DisplayName!;

        var local = contact.Email.Split('@')[0];
        var prefix = local.Length >= 2 ? local[..2] : local;
        return prefix + "***";
    }
}
