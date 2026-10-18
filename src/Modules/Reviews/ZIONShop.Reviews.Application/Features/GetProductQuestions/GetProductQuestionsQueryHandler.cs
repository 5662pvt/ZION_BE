using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Common.Pagination;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.Reviews.Application.Mappings;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.GetProductQuestions;

public class GetProductQuestionsQueryHandler : IRequestHandler<GetProductQuestionsQuery, Result<PagedResult<ProductQuestionDto>>>
{
    private readonly IProductQuestionRepository _questions;

    public GetProductQuestionsQueryHandler(IProductQuestionRepository questions) => _questions = questions;

    public async Task<Result<PagedResult<ProductQuestionDto>>> Handle(GetProductQuestionsQuery request, CancellationToken cancellationToken)
    {
        var query = _questions.Query().AsNoTracking()
            .Where(q => q.ProductId == request.ProductId)
            .OrderByDescending(q => q.CreatedDate);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var dtos = items.Select(q => q.ToDto()).ToList();
        return Result.Success(PagedResult<ProductQuestionDto>.Create(dtos, page, pageSize, total));
    }
}
