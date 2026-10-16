using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.Auth.Domain.Entities;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.Common.Pagination;
using ZIONShop.Contracts.Users;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.ListCustomers;

public class ListCustomersQueryHandler : IRequestHandler<ListCustomersQuery, Result<PagedResult<AdminCustomerDto>>>
{
    private readonly IUserRepository _users;
    private readonly IUserProfileReader _profiles;

    public ListCustomersQueryHandler(IUserRepository users, IUserProfileReader profiles)
    {
        _users = users;
        _profiles = profiles;
    }

    public async Task<Result<PagedResult<AdminCustomerDto>>> Handle(ListCustomersQuery request, CancellationToken cancellationToken)
    {
        IQueryable<User> query = _users.Query().AsNoTracking();

        if (string.Equals(request.Status, "Active", StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => u.IsActive);
        else if (string.Equals(request.Status, "Locked", StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => !u.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var roleUpper = request.Role.Trim().ToUpperInvariant();
            query = query.Where(u => u.RolesRaw.ToUpper().Contains(roleUpper));
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            var kwUpper = kw.ToUpperInvariant();
            var matchingIdsByPhone = await _profiles.FindAuthUserIdsByPhoneAsync(kw, cancellationToken);

            query = query.Where(u =>
                EF.Functions.Like(u.Email.ToUpper(), $"%{kwUpper}%") ||
                (u.DisplayName != null && EF.Functions.Like(u.DisplayName.ToUpper(), $"%{kwUpper}%")) ||
                matchingIdsByPhone.Contains(u.Id));
        }

        query = query.OrderByDescending(u => u.CreatedDate);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.RolesRaw,
                u.IsActive,
                u.EmailConfirmed,
                u.CreatedDate,
                u.LastLoginAt
            })
            .ToListAsync(cancellationToken);

        var ids = items.Select(i => i.Id).ToList();
        var profiles = await _profiles.GetProfilesAsync(ids, cancellationToken);
        var phoneByUserId = profiles.ToDictionary(p => p.AuthUserId, p => p.PhoneNumber);

        var dtos = items.Select(i => new AdminCustomerDto(
            i.Id,
            i.Email,
            i.DisplayName,
            phoneByUserId.TryGetValue(i.Id, out var phone) ? phone : null,
            SplitRoles(i.RolesRaw),
            i.IsActive,
            i.EmailConfirmed,
            i.CreatedDate,
            i.LastLoginAt)).ToList();

        return Result.Success(PagedResult<AdminCustomerDto>.Create(dtos, page, pageSize, total));
    }

    private static IReadOnlyList<string> SplitRoles(string rolesRaw) =>
        string.IsNullOrEmpty(rolesRaw)
            ? Array.Empty<string>()
            : rolesRaw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
