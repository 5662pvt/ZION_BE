using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.Common.Pagination;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.ListCustomers;

public record ListCustomersQuery(
    int Page,
    int PageSize,
    string? Keyword,
    string? Role,
    string? Status) : IRequest<Result<PagedResult<AdminCustomerDto>>>;
