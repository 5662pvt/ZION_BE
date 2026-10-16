using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Payments.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Payments.Application.Features.ListPayments;

public record ListPaymentsQuery(
    int Page,
    int PageSize,
    string? Status,
    string? Channel,
    DateTime? From,
    DateTime? To,
    Guid? OrderId) : IRequest<Result<PagedResult<AdminPaymentSummaryDto>>>;
