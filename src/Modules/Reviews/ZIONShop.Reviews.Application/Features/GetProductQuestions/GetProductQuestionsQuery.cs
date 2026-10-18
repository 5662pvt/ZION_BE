using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.GetProductQuestions;

public record GetProductQuestionsQuery(Guid ProductId, int Page, int PageSize) : IRequest<Result<PagedResult<ProductQuestionDto>>>;
